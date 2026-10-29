using System.ComponentModel.DataAnnotations;
using Kairos.Api.Common.Page;
using Kairos.Api.Common.Result;
using Kairos.Api.Config.UnitOfWork;
using Kairos.Api.Modules.Categories.DTOs.Request;
using Kairos.Api.Modules.Categories.Models;
using Kairos.Api.Modules.Categories.Services.Interface;

namespace Kairos.Api.Modules.Categories.Services.Provider;

public sealed class FilterCategoriesUseCase(IUnitOfWork uow)
    : IFilterCategoriesUseCase
{
    public async Task<Result<Page<CategoryModel>>> ExecuteAsync(
        CategoryFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        if (filter is null)
        {
            return Result<Page<CategoryModel>>.ValidationFailure(
                ["Category filter is required."]);
        }

        var validationResults = new List<ValidationResult>();

        var validationContext = new ValidationContext(filter);

        bool isValid = Validator.TryValidateObject(
            filter,
            validationContext,
            validationResults,
            validateAllProperties: true);

        if (!isValid)
        {
            var errors = validationResults
                .Select(result => result.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!)
                .Distinct()
                .ToArray();

            return Result<Page<CategoryModel>>.ValidationFailure(
                errors.Length > 0
                    ? errors
                    : ["Category filter is invalid."]);
        }

        Page<CategoryModel> categories =
            await uow.CategoryRepository.FilterAsync(
                filter,
                cancellationToken);

        return Result<Page<CategoryModel>>.Success(categories);
    }
}