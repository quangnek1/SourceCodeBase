using Contracts.Common.Messages;

namespace AerationSterilize.Application.Features.V1.Products.Commands.DeleteProduct;
public sealed record DeleteProductCommand(Guid Id) : ICommand;
