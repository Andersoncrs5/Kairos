using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.DTOs.Commands;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Mappings;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public sealed class UpdateCategoryUseCase(IUnitOfWork uow)
    : IUpdateCategoryUseCase
{
    public async Task<Result<CategoryModel>> ExecuteAsync(
        UpdateCategoryCommand command,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        List<ValidationResult> validationResults = [];

        if (!Validator.TryValidateObject(
                command,
                new ValidationContext(command),
                validationResults,
                validateAllProperties: true))
        {
            return Result<CategoryModel>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid update command.")
                    .ToArray(),
                400);
        }

        UpdateCategoryDto dto = command.Dto;

        validationResults = [];

        if (!Validator.TryValidateObject(
                dto,
                new ValidationContext(dto),
                validationResults,
                validateAllProperties: true))
        {
            return Result<CategoryModel>.Failure(
                validationResults
                    .Select(result =>
                        result.ErrorMessage ?? "Invalid category data.")
                    .ToArray(),
                400);
        }

        if (!Guid.TryParse(command.CategoryId, out Guid categoryId))
        {
            return Result<CategoryModel>.Failure(
                ["Id must be a valid UUID."],
                400);
        }

        CategoryModel? category =
            await uow.CategoryRepository.GetByIdAsync(
                categoryId.ToString(),
                ct);

        if (category is null)
        {
            return Result<CategoryModel>.NotFound(
                "Category not found.");
        }

        // -----------------------------------------------------------
        // Validação de código
        // -----------------------------------------------------------

        if (dto.Code is not null)
        {
            string code = dto.Code.Trim();

            if (string.IsNullOrWhiteSpace(code))
            {
                return Result<CategoryModel>.Failure(
                    ["Category code cannot be empty."],
                    400);
            }

            if (!string.Equals(
                    category.Code,
                    code,
                    StringComparison.Ordinal))
            {
                CategoryModel? existingCategory =
                    await uow.CategoryRepository.GetByCodeAsync(
                        code,
                        ct);

                if (existingCategory is not null &&
                    existingCategory.Id != category.Id)
                {
                    return Result<CategoryModel>.Conflict(
                        $"Category with the code '{code}' already exists.");
                }
            }

            dto = new UpdateCategoryDto
            {
                Code = code,
                Name = dto.Name,
                Description = dto.Description,
                Color = dto.Color,
                Icon = dto.Icon,
                DisplayOrder = dto.DisplayOrder,
                IsActive = dto.IsActive
            };
        }

        // -----------------------------------------------------------
        // Validação de nome
        // -----------------------------------------------------------

        if (dto.Name is not null &&
            category.Name != dto.Name)
        {
            bool exists =
                await uow.CategoryRepository.ExistsByNameAsync(
                    dto.Name,
                    ct);

            if (exists)
            {
                return Result<CategoryModel>.Conflict(
                    $"Category with the name '{dto.Name}' already exists.");
            }
        }

        // -----------------------------------------------------------
        // Atualização
        // -----------------------------------------------------------

        CategoryMapper.ApplyUpdate(dto, category);

        uow.CategoryRepository.Update(category);

        return Result<CategoryModel>.Success(category);
    }
}