"""
End-to-end API test: AerationSterilize (identity, products) + CommonServices (master data).

Requires both APIs running (http://localhost:5000, http://localhost:5001), SQL Server (sqlcmd)
and the Redis container "aeration-redis". Every test result is appended to API_TEST_REPORT.md
(solution root) as soon as it runs.

    python tests/ApiTests/run_api_tests.py
"""
import base64
import datetime
import hashlib
import hmac
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

AER = "http://localhost:5000/api/v1"
COM = "http://localhost:5001/api/v1"
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
REPORT = os.path.join(ROOT, "API_TEST_REPORT.md")
AER_SETTINGS = os.path.join(ROOT, "src", "Services", "AerationSterilize", "AerationSterilize.API", "appsettings.json")

SQL_SERVER, SQL_USER, SQL_PASSWORD = "QUANG", "sa", "123456"
AER_DB, COM_DB = "SourceBase", "CommonServices"
REDIS_CONTAINER = "aeration-redis"

PASSWORD = "Test@123"
DEPT_A = "d0000000-0000-0000-0000-00000000000a"
DEPT_B = "d0000000-0000-0000-0000-00000000000b"
ADMIN_ID = "33333333-3333-3333-3333-333333333333"
SEEDED_PRODUCT = "aaaaaaaa-0001-0000-0000-000000000001"

ROLES = {
    "R_MANAGER": ("a0000000-0000-0000-0000-000000000001", "T Product Manager"),
    "R_DEPT": ("a0000000-0000-0000-0000-000000000002", "T Department Staff"),
    "R_OWN": ("a0000000-0000-0000-0000-000000000003", "T Own Staff"),
    "R_VIEWER": ("a0000000-0000-0000-0000-000000000004", "T Master Data Viewer"),
    "R_NONE": ("a0000000-0000-0000-0000-000000000005", "T No Access"),
}
MASTER = ["MASTER_DATA", "LANGUAGE", "REGION", "CURRENCY", "COUNTRY", "CITY", "BANK"]
CRUD = ["VIEW", "CREATE", "UPDATE", "DELETE"]
# role -> [(function, action, AccessLevel)]   AccessLevel: 0 None, 1 Own, 2 Department, 3 All
ROLE_PERMISSIONS = {
    "R_MANAGER": [("PRODUCT", a, 3) for a in CRUD] + [(f, "VIEW", 3) for f in MASTER],
    "R_DEPT": [("PRODUCT", "VIEW", 2), ("PRODUCT", "CREATE", 2), ("PRODUCT", "UPDATE", 2), ("PRODUCT", "DELETE", 1)],
    "R_OWN": [("PRODUCT", a, 1) for a in CRUD],
    "R_VIEWER": [(f, "VIEW", 3) for f in MASTER],
    "R_NONE": [("PRODUCT", "VIEW", 0), ("BANK", "VIEW", 0)],
}
# user -> (department, roles, description)
USERS = {
    "t_manager": (DEPT_A, ["R_MANAGER"], "Phòng A, PRODUCT mức All + xem master data"),
    "t_staff_a1": (DEPT_A, ["R_DEPT"], "Phòng A, PRODUCT VIEW/CREATE/UPDATE mức Department, DELETE mức Own"),
    "t_staff_a2": (DEPT_A, ["R_DEPT"], "Phòng A, giống t_staff_a1"),
    "t_staff_b1": (DEPT_B, ["R_DEPT"], "Phòng B, giống t_staff_a1"),
    "t_own_a1": (DEPT_A, ["R_OWN"], "Phòng A, PRODUCT mức Own"),
    "t_own_a2": (DEPT_A, ["R_OWN"], "Phòng A, PRODUCT mức Own"),
    "t_viewer": (DEPT_B, ["R_VIEWER"], "Phòng B, chỉ xem master data, không có quyền PRODUCT"),
    "t_noaccess": (DEPT_A, ["R_NONE"], "PRODUCT.VIEW và BANK.VIEW nhưng AccessLevel = None"),
    "t_nodept": (None, ["R_DEPT"], "Không thuộc phòng nào, PRODUCT mức Department"),
    "t_multi": (DEPT_B, ["R_OWN", "R_DEPT"], "Phòng B, 2 role Own + Department (lấy mức cao nhất)"),
}

RUN = datetime.datetime.now().strftime("%H%M%S")
stats = {"pass": 0, "fail": 0, "info": 0}
counter = [0]


# ---------------------------------------------------------------- report

def write(text):
    with open(REPORT, "a", encoding="utf-8") as f:
        f.write(text)


def section(title, note=None):
    write(f"\n## {title}\n\n")
    if note:
        write(note + "\n\n")
    write("| # | API | User | Kịch bản | Mong đợi | Thực tế | KQ |\n|---|---|---|---|---|---|---|\n")


def cell(value):
    return str(value).replace("|", "\\|").replace("\n", " ")


def record(api, user, scenario, expected, actual, ok, info=False):
    counter[0] += 1
    mark = "ℹ️" if info else ("✅" if ok else "❌")
    stats["info" if info else ("pass" if ok else "fail")] += 1
    write(f"| {counter[0]} | `{cell(api)}` | {cell(user)} | {cell(scenario)} | {cell(expected)} | {cell(actual)} | {mark} |\n")
    print(f"{mark} {counter[0]:3} {api} [{user}] {scenario} -> {actual}")
    return ok


# ---------------------------------------------------------------- http

class Resp:
    def __init__(self, status, body, headers):
        self.status, self.body, self.headers = status, body, headers

    @property
    def json(self):
        try:
            return json.loads(self.body) if self.body else None
        except ValueError:
            return None

    @property
    def value(self):
        j = self.json
        return j.get("value") if isinstance(j, dict) else None

    def cookie(self, name):
        for k, v in self.headers:
            if k.lower() == "set-cookie" and v.startswith(name + "="):
                return v.split(";", 1)[0].split("=", 1)[1]
        return None

    def short(self):
        j = self.json
        if isinstance(j, dict) and j.get("isFailure"):
            return f"{self.status} ({j['error'].get('code')})"
        if isinstance(j, dict) and "title" in j and self.status >= 400:
            return f"{self.status} ({j.get('title')})"
        return str(self.status)


def http(method, url, token=None, body=None, form=None, cookie=None, lang=None):
    headers = {}
    data = None
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    if form is not None:
        data = urllib.parse.urlencode(form).encode()
        headers["Content-Type"] = "application/x-www-form-urlencoded"
    if cookie:
        headers["Cookie"] = f"refreshToken={cookie}"
    if lang:
        headers["Accept-Language"] = lang
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            return Resp(r.status, r.read().decode(), r.getheaders())
    except urllib.error.HTTPError as e:
        return Resp(e.code, e.read().decode(), e.headers.items())


def expect(method, url, user, scenario, expected_status, token=None, check=None, check_desc=None, **kw):
    """Calls the API, records the row and returns the response."""
    r = http(method, url, token=token, **kw)
    ok = r.status == expected_status
    actual = r.short()
    if ok and check is not None:
        good, detail = check(r)
        ok = ok and good
        actual = f"{actual}; {detail}"
    expected = f"{expected_status}" + (f"; {check_desc}" if check_desc else "")
    record(f"{method} {url.replace(AER, ':5000').replace(COM, ':5001')}", user, scenario, expected, actual, ok)
    return r


# ---------------------------------------------------------------- sql / redis / jwt

def sql(db, query):
    out = subprocess.run(
        ["sqlcmd", "-S", SQL_SERVER, "-U", SQL_USER, "-P", SQL_PASSWORD, "-C", "-d", db, "-b",
         "-h", "-1", "-W", "-s", "|", "-Q", "SET NOCOUNT ON; " + query],
        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if out.returncode != 0:
        raise RuntimeError(f"SQL failed: {out.stdout}{out.stderr}")
    return [line for line in out.stdout.splitlines() if line.strip()]


def redis_delete(pattern):
    keys = subprocess.run(["docker", "exec", REDIS_CONTAINER, "redis-cli", "--scan", "--pattern", pattern],
                          capture_output=True, text=True).stdout.split()
    for k in keys:
        subprocess.run(["docker", "exec", REDIS_CONTAINER, "redis-cli", "del", k], capture_output=True)
    return len(keys)


def b64url(data):
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode()


def jwt_claims(token):
    payload = token.split(".")[1]
    return json.loads(base64.urlsafe_b64decode(payload + "=" * (-len(payload) % 4)))


def jwt_sign(claims, secret):
    header = b64url(json.dumps({"alg": "HS256", "typ": "JWT"}).encode())
    payload = b64url(json.dumps(claims).encode())
    sig = hmac.new(secret.encode(), f"{header}.{payload}".encode(), hashlib.sha256).digest()
    return f"{header}.{payload}.{b64url(sig)}"


def as_list(v):
    return v if isinstance(v, list) else ([] if v is None else [v])


# ---------------------------------------------------------------- setup

def setup():
    write(f"# Báo cáo test API\n\n"
          f"- Thời gian chạy: {datetime.datetime.now():%Y-%m-%d %H:%M:%S}\n"
          f"- AerationSterilize: `{AER}` (DB `{AER_DB}`) — đăng nhập, cấp token, phân quyền, Product\n"
          f"- CommonServices: `{COM}` (DB `{COM_DB}`) — master data, chỉ kiểm tra token của Aeration\n"
          f"- Redis: container `{REDIS_CONTAINER}` (cache + danh sách token đã thu hồi)\n"
          f"- Script: `tests/ApiTests/run_api_tests.py` (chạy lại được, tự dọn dữ liệu test cũ)\n\n"
          f"<!-- SUMMARY -->\n")

    # Clean data from a previous run (test users/roles start with "t_"/"T ", test products with "TEST-").
    role_ids = ",".join(f"'{rid}'" for rid, _ in ROLES.values())
    sql(AER_DB, f"""
        DELETE FROM UserSessions WHERE UserId IN (SELECT Id FROM AppUsers WHERE UserName LIKE 't[_]%');
        DELETE FROM AppUserRoles WHERE UserId IN (SELECT Id FROM AppUsers WHERE UserName LIKE 't[_]%') OR RoleId IN ({role_ids});
        DELETE FROM AppUsers WHERE UserName LIKE 't[_]%';
        DELETE FROM Permissions WHERE RoleId IN ({role_ids});
        DELETE FROM AppRoles WHERE Id IN ({role_ids});
        DELETE FROM Product WHERE Name LIKE 'TEST-%';""")

    # Roles + permissions
    values = []
    for key, (rid, name) in ROLES.items():
        sql(AER_DB, f"INSERT INTO AppRoles (Id, Name, NormalizedName, RoleCode, Description, ConcurrencyStamp) "
                    f"VALUES ('{rid}', N'{name}', N'{name.upper()}', '{key}', N'Role test {key}', NEWID())")
        values += [f"('{rid}','{f}','{a}',{lvl})" for f, a, lvl in ROLE_PERMISSIONS[key]]
    sql(AER_DB, "INSERT INTO Permissions (RoleId, FunctionId, ActionId, AccessLevel) VALUES " + ",".join(values))

    section("0. Chuẩn bị dữ liệu mẫu",
            "Role test (bảng `AppRoles` + `Permissions`, AccessLevel: 0 None, 1 Own, 2 Department, 3 All):\n\n"
            + "\n".join(f"- **{ROLES[k][1]}** (`{k}`): "
                        + ", ".join(f"{f}.{a}:{lvl}" for f, a, lvl in ROLE_PERMISSIONS[k]) for k in ROLES)
            + f"\n\nPhòng ban: A = `{DEPT_A}`, B = `{DEPT_B}`. Mật khẩu mọi user test: `{PASSWORD}`.\n\n"
            + "User test (tạo qua API register, sau đó gán phòng + role bằng SQL):\n\n"
            + "\n".join(f"- `{u}`: {d[2]}" for u, d in USERS.items()))

    ids = {}
    for user, (dept, roles, _) in USERS.items():
        expect("POST", f"{AER}/auth/register", "(anonymous)", f"Đăng ký {user}", 200,
               body={"userName": user, "password": PASSWORD, "email": f"{user}@test.local",
                     "firstName": user, "lastName": "Test", "fullName": f"{user} Test"})
        uid = sql(AER_DB, f"SELECT Id FROM AppUsers WHERE UserName = '{user}'")[0].strip()
        ids[user] = uid
        dept_sql = f"'{dept}'" if dept else "NULL"
        sql(AER_DB, f"UPDATE AppUsers SET DepartmentId = {dept_sql} WHERE Id = '{uid}'; "
                    + "".join(f"INSERT INTO AppUserRoles (UserId, RoleId) VALUES ('{uid}', '{ROLES[r][0]}');" for r in roles))

    record("SQL", "-", "Gán phòng ban + role cho 10 user test", "OK", "OK", True)

    # Permissions are cached in Redis for 30 minutes; drop them so the new roles are used.
    n = redis_delete("aeration:perm:*") + redis_delete("aeration:product:*")
    record("Redis", "-", "Xoá cache quyền + cache product cũ", "OK", f"xoá {n} key", True)
    return ids


# ---------------------------------------------------------------- tests

def login(user, password=PASSWORD):
    r = http("POST", f"{AER}/auth/login", form={"UserName": user, "Password": password})
    return r


def test_auth(ids):
    section("1. Đăng nhập / token (AerationSterilize)")
    tokens, cookies = {}, {}

    expect("POST", f"{AER}/auth/register", "(anonymous)", "Đăng ký trùng username", 400,
           body={"userName": "t_manager", "password": PASSWORD, "email": "other@test.local",
                 "firstName": "x", "lastName": "x", "fullName": "x"})
    r = http("POST", f"{AER}/auth/login", form={"UserName": "admin", "Password": "sai-mat-khau"})
    record("POST :5000/auth/login", "admin", "Sai mật khẩu", "4xx", r.short(), 400 <= r.status < 500)
    r = http("POST", f"{AER}/auth/login", form={"UserName": "khong_ton_tai", "Password": "x"})
    record("POST :5000/auth/login", "khong_ton_tai", "User không tồn tại", "4xx", r.short(), 400 <= r.status < 500)

    for user in ["admin"] + list(USERS):
        r = login(user, "Admin@123" if user == "admin" else PASSWORD)
        ok = r.status == 200 and r.json and r.json.get("accessToken")
        detail = r.short()
        if ok:
            tokens[user] = r.json["accessToken"]
            cookies[user] = r.cookie("refreshToken")
            c = jwt_claims(tokens[user])
            levels = [x for x in as_list(c.get("permission_level")) if x.startswith("PRODUCT.") or x.startswith("BANK.VIEW")]
            detail += f"; department_id={c.get('department_id')}; {', '.join(sorted(levels)) or '(không có quyền PRODUCT/BANK)'}"
        record("POST :5000/auth/login", user, "Đăng nhập, xem claim trong token", "200 + token", detail, bool(ok))

    # Claim checks
    c = jwt_claims(tokens["admin"])
    record("JWT", "admin", "Quyền cũ của admin sau migration FixPermissionAccessLevel", "PRODUCT.VIEW:3",
           "có" if "PRODUCT.VIEW:3" in as_list(c.get("permission_level")) else "không có",
           "PRODUCT.VIEW:3" in as_list(c.get("permission_level")))
    c = jwt_claims(tokens["t_staff_a1"])
    record("JWT", "t_staff_a1", "Token có department_id của phòng A", DEPT_A, c.get("department_id"),
           c.get("department_id") == DEPT_A)
    c = jwt_claims(tokens["t_multi"])
    record("JWT", "t_multi", "2 role Own + Department → lấy mức cao nhất", "PRODUCT.VIEW:2",
           [x for x in as_list(c.get("permission_level")) if x.startswith("PRODUCT.VIEW")],
           "PRODUCT.VIEW:2" in as_list(c.get("permission_level")))
    c = jwt_claims(tokens["t_nodept"])
    record("JWT", "t_nodept", "User không có phòng → không có claim department_id", "không có",
           c.get("department_id") or "không có", "department_id" not in c)
    c = jwt_claims(tokens["admin"])
    record("JWT", "admin", "Issuer / Audience", "AerationSterilize", f"{c.get('iss')} / {c.get('aud')}",
           c.get("iss") == "AerationSterilize" and c.get("aud") == "AerationSterilize")

    expect("GET", f"{AER}/auth/me", "(anonymous)", "Không có token", 401)
    expect("GET", f"{AER}/auth/me", "t_staff_a1", "Có token", 200, token=tokens["t_staff_a1"])

    expect("POST", f"{AER}/auth/refresh-token", "(anonymous)", "Không có cookie refreshToken", 401)
    used_cookie = cookies["t_own_a1"]
    r = expect("POST", f"{AER}/auth/refresh-token", "t_own_a1", "Refresh bằng cookie", 200, cookie=cookies["t_own_a1"],
               check=lambda r: (bool(r.value and r.value.get("accessToken")), "nhận access token mới"),
               check_desc="access token mới")
    if r.status == 200 and r.value:
        new = r.value["accessToken"]
        c = jwt_claims(new)
        record("JWT", "t_own_a1", "Token sau refresh vẫn có department_id + quyền", f"{DEPT_A}; PRODUCT.VIEW:1",
               f"{c.get('department_id')}; {[x for x in as_list(c.get('permission_level')) if x.startswith('PRODUCT.VIEW')]}",
               c.get("department_id") == DEPT_A and "PRODUCT.VIEW:1" in as_list(c.get("permission_level")))
        tokens["t_own_a1"] = new
        cookies["t_own_a1"] = urllib.parse.quote(r.value["refreshToken"], safe="")
        record("POST :5000/auth/refresh-token", "t_own_a1", "Refresh token mới trả trong body, KHÔNG set lại cookie",
               "-", "cookie mới: " + ("có" if r.cookie("refreshToken") else "không"), True, info=True)
    r = http("POST", f"{AER}/auth/refresh-token", cookie=used_cookie)
    record("POST :5000/auth/refresh-token", "t_own_a1", "Dùng lại refresh token cũ (đã bị thu hồi)", "4xx", r.short(),
           400 <= r.status < 500)
    return tokens, cookies


def test_product_authorization(tokens):
    section("2. Product — chặn theo quyền chức năng ([Permission])")
    body = {"name": "TEST-hack", "price": 1, "description": "x"}
    expect("GET", f"{AER}/products?sortColumn=name", "(anonymous)", "Không có token", 401)
    expect("POST", f"{AER}/products", "(anonymous)", "Tạo product không token (trước đây KHÔNG bị chặn)", 401, body=body)
    expect("PUT", f"{AER}/products/{SEEDED_PRODUCT}", "(anonymous)", "Sửa product không token (trước đây KHÔNG bị chặn)", 401, body=body)
    expect("DELETE", f"{AER}/products/{SEEDED_PRODUCT}", "(anonymous)", "Xoá product không token (trước đây KHÔNG bị chặn)", 401)
    expect("GET", f"{AER}/products?sortColumn=name", "(token rác)", "Token không hợp lệ", 401, token="abc.def.ghi")
    expect("GET", f"{AER}/products?sortColumn=name", "t_viewer", "Không có quyền PRODUCT.VIEW", 403, token=tokens["t_viewer"])
    expect("POST", f"{AER}/products", "t_viewer", "Không có quyền PRODUCT.CREATE", 403, token=tokens["t_viewer"], body=body)
    expect("GET", f"{AER}/products?sortColumn=name", "t_noaccess", "Có PRODUCT.VIEW nhưng AccessLevel = None", 403, token=tokens["t_noaccess"])
    expect("POST", f"{AER}/products", "t_manager", "Validation: tên rỗng", 400, token=tokens["t_manager"],
           body={"name": "", "price": 1, "description": "x"})
    expect("POST", f"{AER}/products", "t_manager", "Validation: giá âm", 400, token=tokens["t_manager"],
           body={"name": "TEST-x", "price": -1, "description": "x"})


def test_product_scope(tokens, ids):
    section("3. Product — phân quyền theo dữ liệu (AccessLevel)",
            "Mỗi user tạo 1 product. Sau đó từng user xem / sửa / xoá để kiểm tra phạm vi All / Department / Own.")
    creators = {"TEST-M": "t_manager", "TEST-A1": "t_staff_a1", "TEST-A2": "t_staff_a2", "TEST-B1": "t_staff_b1",
                "TEST-O1": "t_own_a1", "TEST-O2": "t_own_a2", "TEST-MULTI": "t_multi"}
    pid = {}
    for name, user in creators.items():
        r = expect("POST", f"{AER}/products", user, f"Tạo {name}", 200, token=tokens[user],
                   body={"name": name, "price": 100, "description": f"Product test của {user}"})
        pid[name] = r.value

    # Owner columns filled from the token
    rows = sql(AER_DB, "SELECT Name, CreatedBy, ISNULL(CAST(DepartmentId AS varchar(36)),'NULL') FROM Product WHERE Name LIKE 'TEST-%'")
    bad = []
    for row in rows:
        name, created_by, dept = [x.strip() for x in row.split("|")]
        user = creators[name]
        exp_dept = USERS[user][0] or "NULL"
        if created_by.lower() != ids[user].lower() or dept.lower() != exp_dept.lower():
            bad.append(name)
    record("SQL Product", "-", "CreatedBy / DepartmentId được gán theo user tạo", "đúng cho 7 product",
           "sai: " + ", ".join(bad) if bad else f"đúng cho {len(rows)} product", not bad and len(rows) == 7)

    def names(r):
        return sorted(x["name"] for x in (r.value or {}).get("items", []))

    expected_lists = {
        "admin": ["TEST-A1", "TEST-A2", "TEST-B1", "TEST-M", "TEST-MULTI", "TEST-O1", "TEST-O2"],
        "t_manager": ["TEST-A1", "TEST-A2", "TEST-B1", "TEST-M", "TEST-MULTI", "TEST-O1", "TEST-O2"],
        "t_staff_a1": ["TEST-A1", "TEST-A2", "TEST-M", "TEST-O1", "TEST-O2"],
        "t_staff_b1": ["TEST-B1", "TEST-MULTI"],
        "t_own_a1": ["TEST-O1"],
        "t_own_a2": ["TEST-O2"],
        "t_multi": ["TEST-B1", "TEST-MULTI"],
        "t_nodept": [],
    }
    scope_desc = {"admin": "All", "t_manager": "All", "t_staff_a1": "Department A", "t_staff_b1": "Department B",
                  "t_own_a1": "Own", "t_own_a2": "Own", "t_multi": "Department B (max Own/Dept)",
                  "t_nodept": "Department nhưng không có phòng"}
    url = f"{AER}/products?searchTerm=TEST-&sortColumn=name&pageSize=100"
    for user, exp in expected_lists.items():
        expect("GET", url, user, f"Danh sách, phạm vi {scope_desc[user]} (cùng query → cache key tách theo phạm vi)", 200,
               token=tokens[user], check=lambda r, e=exp: (names(r) == e, ", ".join(names(r)) or "(rỗng)"),
               check_desc=", ".join(exp) or "(rỗng)")

    all_url = f"{AER}/products?sortColumn=name&pageSize=100"
    expect("GET", all_url, "admin", "Danh sách không lọc tên: thấy cả product seed (không có chủ)", 200, token=tokens["admin"],
           check=lambda r: (SEEDED_PRODUCT in [x["id"] for x in r.value["items"]], f"{r.value['totalCount']} product"),
           check_desc="có product seed")
    expect("GET", all_url, "t_staff_a1", "Danh sách không lọc tên: KHÔNG thấy product seed (không thuộc phòng A)", 200,
           token=tokens["t_staff_a1"],
           check=lambda r: (SEEDED_PRODUCT not in [x["id"] for x in r.value["items"]], f"{r.value['totalCount']} product"),
           check_desc="không có product seed")

    def by_id(user, name, status, scenario):
        target = pid.get(name, name)
        expect("GET", f"{AER}/products/{target}", user, scenario, status, token=tokens[user])

    by_id("t_staff_a2", "TEST-A1", 200, "Xem product của đồng nghiệp cùng phòng A")
    by_id("t_staff_b1", "TEST-A1", 404, "Xem product phòng A từ phòng B")
    by_id("t_own_a1", "TEST-O1", 200, "Xem product của chính mình")
    by_id("t_own_a1", "TEST-O2", 404, "Xem product của người khác (mức Own)")
    by_id("t_nodept", "TEST-A1", 404, "User không có phòng xem product phòng A")
    by_id("t_manager", SEEDED_PRODUCT, 200, "Mức All xem product seed")
    by_id("t_staff_a1", SEEDED_PRODUCT, 404, "Mức Department xem product seed (không có phòng)")
    by_id("admin", str(uuid.uuid4()), 404, "Id không tồn tại")

    def update(user, name, status, scenario, new_name=None):
        return expect("PUT", f"{AER}/products/{pid[name]}", user, scenario, status, token=tokens[user],
                      body={"name": new_name or name, "price": 200, "description": f"Sửa bởi {user}"})

    update("t_own_a2", "TEST-O1", 404, "Sửa product của người khác (mức Own)")
    update("t_own_a1", "TEST-O1", 200, "Sửa product của chính mình")
    update("t_staff_b1", "TEST-A1", 404, "Sửa product phòng A từ phòng B")
    update("t_staff_a2", "TEST-A1", 200, "Sửa product của đồng nghiệp cùng phòng (UPDATE mức Department)", "TEST-A1-UPD")
    expect("GET", f"{AER}/products/{pid['TEST-A1']}", "t_staff_a1", "Xem lại sau khi sửa (cache đã bị xoá)", 200,
           token=tokens["t_staff_a1"], check=lambda r: (r.value["name"] == "TEST-A1-UPD", r.value["name"]),
           check_desc="TEST-A1-UPD")

    def delete(user, name, status, scenario):
        expect("DELETE", f"{AER}/products/{pid[name]}", user, scenario, status, token=tokens[user])

    delete("t_staff_a2", "TEST-A1", 404, "Xoá product của đồng nghiệp (DELETE chỉ mức Own)")
    delete("t_staff_b1", "TEST-A1", 404, "Xoá product phòng khác")
    delete("t_staff_a1", "TEST-A1", 200, "Xoá product của chính mình")
    delete("t_own_a2", "TEST-O1", 404, "Xoá product của người khác (mức Own)")
    delete("t_manager", "TEST-O1", 200, "Mức All xoá product của người khác")
    expect("GET", url, "t_staff_a1", "Danh sách phòng A sau khi xoá", 200, token=tokens["t_staff_a1"],
           check=lambda r: (names(r) == ["TEST-A2", "TEST-M", "TEST-O2"], ", ".join(names(r))),
           check_desc="TEST-A2, TEST-M, TEST-O2")


def test_permission_change(tokens, cookies, ids):
    section("4. Đổi quyền khi user đang đăng nhập",
            "Quyền nằm trong token (60 phút) và danh sách quyền được cache trên Redis 30 phút (`aeration:perm:{userId}`).")
    viewer = "t_viewer"
    sql(AER_DB, f"INSERT INTO Permissions (RoleId, FunctionId, ActionId, AccessLevel) VALUES ('{ROLES['R_VIEWER'][0]}','PRODUCT','VIEW',3)")
    record("SQL", "-", "Cấp thêm PRODUCT.VIEW mức All cho role Master Data Viewer", "OK", "OK", True)
    expect("GET", f"{AER}/products?sortColumn=name", viewer, "Token cũ (chưa có quyền mới)", 403, token=tokens[viewer])

    r = http("POST", f"{AER}/auth/refresh-token", cookie=cookies[viewer])
    new = r.value["accessToken"] if r.status == 200 else None
    status = http("GET", f"{AER}/products?sortColumn=name", token=new).status if new else None
    record("POST :5000/auth/refresh-token → GET :5000/products", viewer,
           "Refresh ngay sau khi đổi quyền (quyền cũ còn trong cache Redis)", "-",
           f"refresh {r.status}, products {status} " + ("(vẫn dùng quyền cũ trong cache)" if status == 403 else ""),
           True, info=True)

    n = redis_delete(f"aeration:perm:{ids[viewer].lower()}") + redis_delete(f"aeration:perm:{ids[viewer].upper()}")
    record("Redis", "-", "Xoá cache quyền của t_viewer", "OK", f"xoá {n} key", True)
    cookie = urllib.parse.quote(r.value["refreshToken"], safe="") if r.status == 200 else cookies[viewer]
    r = http("POST", f"{AER}/auth/refresh-token", cookie=cookie)
    new = r.value["accessToken"] if r.status == 200 else None
    expect("GET", f"{AER}/products?sortColumn=name", viewer, "Refresh lại sau khi xoá cache → có quyền mới", 200, token=new)


def test_common_auth(tokens):
    section("5. CommonServices — xác thực bằng token của Aeration")
    expect("GET", f"{COM}/banks", "(anonymous)", "Không có token", 401)
    expect("POST", f"{COM}/auth/login", "admin", "Login cũ của CommonServices đã bị bỏ", 404,
           form={"UserName": "admin", "Password": "Admin@123"})

    claims = jwt_claims(tokens["admin"])
    old = dict(claims, iss="CommonServices", aud="CommonServices", jti=str(uuid.uuid4()))
    forged = jwt_sign(old, "CommonServices_SuperSecretKey_PhaiDaiHon_32KyTu_Nhe_123456!")
    expect("GET", f"{COM}/banks", "admin (token kiểu cũ)", "Token ký bằng secret/issuer cũ của CommonServices", 401, token=forged)

    secret = json.load(open(AER_SETTINGS, encoding="utf-8-sig"))["JwtOptions"]["SecretKey"]
    expired = jwt_sign(dict(claims, exp=int(time.time()) - 60, jti=str(uuid.uuid4())), secret)
    expect("GET", f"{COM}/banks", "admin", "Token hết hạn", 401, token=expired)
    head, payload, sig = tokens["admin"].split(".")
    tampered_claims = dict(claims, permission=as_list(claims["permission"]) + ["X.Y"])
    tampered = f"{head}.{b64url(json.dumps(tampered_claims).encode())}.{sig}"
    expect("GET", f"{COM}/banks", "admin", "Token bị sửa payload (sai chữ ký)", 401, token=tampered)
    expect("GET", f"{COM}/banks", "admin", "Token của Aeration", 200, token=tokens["admin"])

    section("6. CommonServices — quyền chức năng")
    for res in ["languages", "regions", "currencies", "countries", "cities", "banks"]:
        expect("GET", f"{COM}/{res}", "t_viewer", f"Xem {res} (có quyền VIEW)", 200, token=tokens["t_viewer"])
    expect("POST", f"{COM}/banks", "t_viewer", "Tạo bank khi chỉ có quyền VIEW", 403, token=tokens["t_viewer"],
           body={"code": "X", "name": "X", "isVAByBank": False})
    expect("GET", f"{COM}/banks", "t_staff_a1", "Không có quyền BANK.VIEW", 403, token=tokens["t_staff_a1"])
    expect("GET", f"{COM}/banks", "t_noaccess", "BANK.VIEW nhưng AccessLevel = None", 403, token=tokens["t_noaccess"])
    expect("GET", f"{COM}/masterdata/banks", "(anonymous)", "MasterData không token", 401)
    for res in ["languages", "languages/native", "regions", "currencies", "countries", "cities", "banks"]:
        expect("GET", f"{COM}/masterdata/{res}", "t_staff_a1", f"MasterData/{res}: mọi user đã đăng nhập", 200,
               token=tokens["t_staff_a1"])


def test_common_crud(tokens):
    section("7. CommonServices — CRUD master data (admin)",
            f"Mã dữ liệu test có hậu tố `{RUN}` để chạy lại không bị trùng (xoá là xoá mềm, mã vẫn giữ unique).")
    t = tokens["admin"]
    ids = {}

    def crud(res, create_body, update_body, label):
        r = expect("POST", f"{COM}/{res}", "admin", f"Tạo {label}", 200, token=t, body=create_body)
        rid = r.value
        ids[res] = rid
        if not rid:
            return None
        expect("GET", f"{COM}/{res}/{rid}", "admin", f"Xem {label} theo id", 200, token=t)
        expect("PUT", f"{COM}/{res}/{rid}", "admin", f"Sửa {label}", 200, token=t, body=update_body,
               check=lambda r: (r.value and r.value.get("name") == update_body["name"], r.value and r.value.get("name")),
               check_desc=update_body["name"])
        expect("PUT", f"{COM}/{res}/{rid}/deactivate", "admin", f"Ngừng kích hoạt {label}", 200, token=t)
        expect("GET", f"{COM}/{res}/list?status=0", "admin", f"Danh sách {label} trạng thái Inactive", 200, token=t,
               check=lambda r: (rid in [x["id"] for x in r.value], "có" if rid in [x["id"] for x in r.value] else "không có"),
               check_desc="có bản ghi vừa ngừng")
        expect("PUT", f"{COM}/{res}/{rid}/activate", "admin", f"Kích hoạt lại {label}", 200, token=t)
        expect("GET", f"{COM}/{res}?pageSize=100", "admin", f"Danh sách phân trang {label}", 200, token=t,
               check=lambda r: (rid in [x["id"] for x in r.value["items"]], f"{r.value['totalCount']} bản ghi"),
               check_desc="có bản ghi mới")
        return rid

    tr = lambda vi, en: [{"langCode": "vi-VN", "name": vi}, {"langCode": "en-US", "name": en}]
    crud("regions", {"code": f"R{RUN}", "name": f"Vùng test {RUN}", "note": "test", "sortOrder": 9},
         {"code": f"R{RUN}", "name": f"Vùng test {RUN} (sửa)", "note": "test", "sortOrder": 9}, "region")
    crud("currencies", {"code": f"C{RUN}", "name": f"Tiền test {RUN}", "symbol": "T", "isCurrencyOptionEnabled": True},
         {"code": f"C{RUN}", "name": f"Tiền test {RUN} (sửa)", "symbol": "T", "isCurrencyOptionEnabled": False}, "currency")
    crud("countries", {"code": f"N{RUN}", "name": f"Nước test {RUN}", "regionId": ids.get("regions"), "currencyId": ids.get("currencies")},
         {"code": f"N{RUN}", "name": f"Nước test {RUN} (sửa)", "regionId": ids.get("regions"), "currencyId": ids.get("currencies")}, "country")
    crud("cities", {"code": f"T{RUN}", "name": f"Thành phố test {RUN}", "countryId": ids.get("countries")},
         {"code": f"T{RUN}", "name": f"Thành phố test {RUN} (sửa)", "countryId": ids.get("countries")}, "city")
    crud("languages", {"code": f"t-{RUN}", "name": f"Ngôn ngữ test {RUN}", "platform": 99},
         {"code": f"t-{RUN}", "name": f"Ngôn ngữ test {RUN} (sửa)", "platform": 1}, "language")
    crud("banks", {"code": f"B{RUN}", "name": f"Ngân hàng test {RUN}", "vaPrefix": "99", "isVAByBank": True,
                   "translations": tr(f"Ngân hàng test {RUN}", f"Test bank {RUN}")},
         {"code": f"B{RUN}", "name": f"Ngân hàng test {RUN} (sửa)", "isVAByBank": False,
          "translations": tr(f"Ngân hàng test {RUN} (sửa)", f"Test bank {RUN} (edited)")}, "bank")

    if ids.get("banks"):
        expect("GET", f"{COM}/banks/list", "admin", "Accept-Language: en-US → tên tiếng Anh", 200, token=t, lang="en-US",
               check=lambda r: (any(x["name"] == f"Test bank {RUN} (edited)" for x in r.value),
                                next((x["name"] for x in r.value if x["id"] == ids["banks"]), "?")),
               check_desc=f"Test bank {RUN} (edited)")
        created_by = sql(COM_DB, f"SELECT CreatedBy FROM Banks WHERE Id = '{ids['banks']}'")[0].strip()
        record("SQL Banks", "admin", "CreatedBy lấy từ token Aeration (user id của admin)", ADMIN_ID, created_by,
               created_by.lower() == ADMIN_ID)
    expect("POST", f"{COM}/banks", "admin", "Tạo bank trùng mã", 400, token=t,
           body={"code": f"B{RUN}", "name": "trùng", "isVAByBank": False})
    expect("POST", f"{COM}/banks", "admin", "Validation: mã + tên rỗng", 400, token=t, body={"code": "", "name": ""})
    expect("POST", f"{COM}/cities", "admin", "Tạo city với country không tồn tại", 404, token=t,
           body={"code": f"X{RUN}", "name": "x", "countryId": str(uuid.uuid4())})

    for res, label in [("cities", "city"), ("countries", "country"), ("currencies", "currency"), ("regions", "region"),
                       ("languages", "language"), ("banks", "bank")]:
        if ids.get(res):
            expect("DELETE", f"{COM}/{res}/{ids[res]}", "admin", f"Xoá {label}", 200, token=t)
            expect("GET", f"{COM}/{res}/{ids[res]}", "admin", f"Xem {label} sau khi xoá", 404, token=t)


def test_logout():
    section("8. Logout — thu hồi token trên cả 2 service")
    r = login("t_manager")
    token, cookie = r.json["accessToken"], r.cookie("refreshToken")
    expect("GET", f"{AER}/products?sortColumn=name", "t_manager", "Trước logout", 200, token=token)
    expect("GET", f"{COM}/banks", "t_manager", "Trước logout (CommonServices)", 200, token=token)
    expect("POST", f"{AER}/auth/logout", "(anonymous)", "Logout không có token", 401, cookie=cookie)
    expect("POST", f"{AER}/auth/logout", "t_manager", "Logout", 200, token=token, cookie=cookie)
    expect("GET", f"{AER}/products?sortColumn=name", "t_manager", "Token đã logout gọi Aeration", 401, token=token)
    expect("GET", f"{COM}/banks", "t_manager", "Token đã logout gọi CommonServices (danh sách thu hồi dùng chung)", 401, token=token)
    r = http("POST", f"{AER}/auth/refresh-token", cookie=cookie)
    record("POST :5000/auth/refresh-token", "t_manager", "Refresh bằng cookie đã logout", "4xx", r.short(), 400 <= r.status < 500)


def finish():
    total = stats["pass"] + stats["fail"]
    summary = (f"## Tổng kết\n\n- Tổng số test: **{total}** — ✅ đạt **{stats['pass']}**, ❌ lỗi **{stats['fail']}**"
               f"{', ℹ️ ghi nhận ' + str(stats['info']) if stats['info'] else ''}\n"
               f"- Ký hiệu: ✅ đúng như mong đợi, ❌ sai, ℹ️ ghi nhận hành vi (không tính đạt/lỗi)\n")
    content = open(REPORT, encoding="utf-8").read().replace("<!-- SUMMARY -->\n", summary)
    open(REPORT, "w", encoding="utf-8").write(content)
    print(f"\nPASS {stats['pass']}  FAIL {stats['fail']}  INFO {stats['info']}  -> {REPORT}")


def main():
    sys.stdout.reconfigure(encoding="utf-8")
    open(REPORT, "w", encoding="utf-8").close()
    ids = setup()
    tokens, cookies = None, None
    try:
        tokens, cookies = test_auth(ids)
        test_product_authorization(tokens)
        test_product_scope(tokens, ids)
        test_permission_change(tokens, cookies, ids)
        test_common_auth(tokens)
        test_common_crud(tokens)
        test_logout()
    except Exception as ex:  # keep what was written so far and show where it stopped
        record("-", "-", "Script dừng do lỗi", "-", f"{type(ex).__name__}: {ex}", False)
        raise
    finally:
        finish()


if __name__ == "__main__":
    main()
