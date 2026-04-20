using FluentValidation;
using SRN.Application.DTOs;

namespace SRN.Application.Validators
{
    /// <summary>
    /// Defines strict validation rules for incoming document uploads using FluentValidation.
    /// This acts as a robust firewall, intercepting bad or malicious requests before they reach the controller logic.
    /// </summary>
    public class ArtifactUploadDtoValidator : AbstractValidator<ArtifactUploadDto>
    {
        public ArtifactUploadDtoValidator()
        {
            // Title constraints to ensure UI rendering consistency
            RuleFor(x => x.Title)
                .NotEmpty().WithMessage("Title is required.")
                .MaximumLength(100).WithMessage("Title must not exceed 100 characters.");

            // Strict file validation constraints
            RuleFor(x => x.File)
                .NotNull().WithMessage("File is required.")
                .Must(file => file.Length > 0).WithMessage("File content cannot be empty.")
                // Security Constraint: Hard limit of 10MB to prevent Denial of Service (DoS) attacks via massive file uploads
                .Must(file => file.Length <= 10 * 1024 * 1024).WithMessage("File size must be less than 10MB.")
                // Format Constraint: Restricts uploads strictly to PDF documents
                .Must(file => file.ContentType == "application/pdf").WithMessage("Only PDF files are allowed.");
        }
    }
}