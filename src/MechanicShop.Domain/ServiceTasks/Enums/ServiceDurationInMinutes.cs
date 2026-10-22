namespace MechanicShop.Domain.ServiceTasks.Enums;

/// <summary>
/// Fixed service durations on a 15-minute grid. The catalog needs a small, stable
/// set of values so the F4 scheduler can place tasks into calendar slots without
/// parsing arbitrary ints from user input.
/// </summary>
public enum ServiceDurationInMinutes
{
    Min15 = 15,
    Min30 = 30,
    Min45 = 45,
    Min60 = 60,
    Min75 = 75,
    Min90 = 90,
    Min105 = 105,
    Min120 = 120,
    Min135 = 135,
    Min150 = 150,
    Min165 = 165,
    Min180 = 180
}
