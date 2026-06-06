using AutoMapper;

namespace Contracts.Mapping;
public interface IMapFrom<T>
{
    void Mapping(Profile profile) => profile.CreateMap(typeof(T), GetType());
}
