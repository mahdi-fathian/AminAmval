using MediatR;
using AminAmval.Application.DTOs;

namespace AminAmval.Application.Queries.References;

public sealed record GetCategoriesQuery : IRequest<IReadOnlyList<CategoryDto>>;

public sealed record GetDepartmentsQuery : IRequest<IReadOnlyList<DepartmentDto>>;

public sealed record GetLookupsQuery : IRequest<LookupsDto>;

public sealed record GetOwnersQuery : IRequest<IReadOnlyList<string>>;