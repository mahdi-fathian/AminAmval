using MediatR;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Commands.References;

public sealed record CreateCategoryCommand(CreateCategoryRequest Request) : IRequest<string>;

public sealed record UpdateCategoryCommand(string Id, UpdateCategoryRequest Request) : IRequest<Unit>;

public sealed record DeleteCategoryCommand(string Id, string Reason) : IRequest<Unit>;

public sealed record CreateDepartmentCommand(CreateDepartmentRequest Request) : IRequest<string>;

public sealed record UpdateDepartmentCommand(string Id, UpdateDepartmentRequest Request) : IRequest<Unit>;

public sealed record DeleteDepartmentCommand(string Id, string Reason) : IRequest<Unit>;