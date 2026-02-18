using FluentValidation;
using SRN.API.DTOs;

namespace SRN.API.Validators
{
    public class ArtifactUploadDtoValidator : AbstractValidator<ArtifactUploadDto>
    {
        public ArtifactUploadDtoValidator()
        {
            // 1. 验证标题：不能为空，长度限制
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");

            // 2. 验证文件：不能为空，大小限制(比如 10MB)
            RuleFor(x => x.File)
                .NotNull().WithMessage("File is required.")
                .Must(file => file.Length > 0).WithMessage("File content cannot be empty.")
                .Must(file => file.Length <= 10 * 1024 * 1024).WithMessage("File size must be less than 10MB.")
                .Must(file => file.ContentType == "application/pdf").WithMessage("Only PDF files are allowed."); // 可选：限制格式
        }
    }
}