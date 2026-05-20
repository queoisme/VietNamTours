using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class CreateTourRequest
{
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string LocationCity { get; set; } = default!;
    public string? LocationAddress { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal PricePerPerson { get; set; }
    public decimal DurationHours { get; set; }
    public short MaxGroupSize { get; set; } = 10;
    public string[] Highlights { get; set; } = [];
    public string[] Included { get; set; } = [];
    public string[] Excluded { get; set; } = [];
    public List<ItineraryItemRequest> Itinerary { get; set; } = [];
    public string[] Images { get; set; } = [];
    public string? CoverImageUrl { get; set; }
}

public class UpdateTourRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? LocationCity { get; set; }
    public string? LocationAddress { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal? PricePerPerson { get; set; }
    public decimal? DurationHours { get; set; }
    public short? MaxGroupSize { get; set; }
    public string[]? Highlights { get; set; }
    public string[]? Included { get; set; }
    public string[]? Excluded { get; set; }
    public List<ItineraryItemRequest>? Itinerary { get; set; }
    public string[]? Images { get; set; }
    public string? CoverImageUrl { get; set; }
}

public class UpdateTourStatusRequest
{
    public string Status { get; set; } = default!;
}

public class ItineraryItemRequest
{
    public string Time { get; set; } = default!;
    public string Activity { get; set; } = default!;
    public string? Description { get; set; }
}

public class TourSearchParams
{
    public string? Q { get; set; }
    public string? City { get; set; }
    public string? Category { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinRating { get; set; }
    public decimal? MinDuration { get; set; }
    public decimal? MaxDuration { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 20;
}

public class CreateAvailabilityRequest
{
    public DateOnly AvailableDate { get; set; }
    public short MaxSlots { get; set; }
    public bool IsBlocked { get; set; }
}

public class UpdateAvailabilityRequest
{
    public short? MaxSlots { get; set; }
    public bool? IsBlocked { get; set; }
}

// --- Validators ---

public class CreateTourRequestValidator : AbstractValidator<CreateTourRequest>
{
    private static readonly string[] ValidCategories = ["nature", "culture", "food", "resort", "adventure", "other"];

    public CreateTourRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty();
        RuleFor(x => x.Category).NotEmpty().Must(c => ValidCategories.Contains(c.ToLower()))
            .WithMessage("Category must be one of: nature, culture, food, resort, adventure, other");
        RuleFor(x => x.LocationCity).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PricePerPerson).GreaterThan(0);
        RuleFor(x => x.DurationHours).GreaterThan(0);
        RuleFor(x => x.MaxGroupSize).GreaterThan((short)0);
        RuleFor(x => x.Images).Must(imgs => imgs.Length <= 10).WithMessage("Maximum 10 images allowed");
    }
}

public class UpdateTourStatusRequestValidator : AbstractValidator<UpdateTourStatusRequest>
{
    private static readonly string[] ValidStatuses = ["draft", "active", "inactive"];

    public UpdateTourStatusRequestValidator()
    {
        RuleFor(x => x.Status).NotEmpty().Must(s => ValidStatuses.Contains(s.ToLower()))
            .WithMessage("Status must be one of: draft, active, inactive");
    }
}

public class CreateAvailabilityRequestValidator : AbstractValidator<CreateAvailabilityRequest>
{
    public CreateAvailabilityRequestValidator()
    {
        RuleFor(x => x.AvailableDate).GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Available date must be today or in the future");
        RuleFor(x => x.MaxSlots).GreaterThan((short)0).When(x => !x.IsBlocked);
    }
}

public class RemoveImageRequest
{
    public string Url { get; set; } = default!;
}
