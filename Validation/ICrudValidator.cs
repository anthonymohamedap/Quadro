using System.Threading.Tasks;

namespace QuadroApp.Validation;

public interface ICrudValidator<T>
{
    Task<ValidationResult> ValidateCreateAsync(T entity);
    Task<ValidationResult> ValidateUpdateAsync(T entity);
    Task<ValidationResult> ValidateDeleteAsync(T entity);
}
