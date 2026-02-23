using FluentValidation;
using SRN.Application.DTOs;

namespace SRN.Application.Validators
{
    public class ArtifactUploadDtoValidator : AbstractValidator<ArtifactUploadDto>
    {
        public ArtifactUploadDtoValidator()
        {
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");

            RuleFor(x => x.File)
                .NotNull().WithMessage("File is required.")
                .Must(file => file.Length > 0).WithMessage("File content cannot be empty.")
                .Must(file => file.Length <= 10 * 1024 * 1024).WithMessage("File size must be less than 10MB.")
                .Must(file => file.ContentType == "application/pdf").WithMessage("Only PDF files are allowed.");
        }
    }
}