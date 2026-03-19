namespace StableDiffusionStudio.Application.Commands;

public record RenameProjectCommand(Guid Id, string NewName);
