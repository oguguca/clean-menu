namespace CleanMenu.Core.Maintenance;

/// <summary>A Windows power plan.</summary>
public sealed record PowerPlan(Guid Id, string Name, bool IsActive)
{
    /// <summary>Plain explanation of the built-in plans.</summary>
    public string Explanation => Id.ToString().ToUpperInvariant() switch
    {
        "381B4222-F694-41F0-9685-FF5BB260DF2E" => "Equilibra desempenho e economia de energia. Recomendado para a maioria.",
        "8C5E7FDA-E8BF-4A96-9A85-A6E23A8C635C" => "Mais desempenho, mais consumo de energia e calor. Em notebooks, a bateria dura menos.",
        "A1841308-3541-4FAB-BC81-F71556F20B4A" => "Economiza energia reduzindo o desempenho.",
        "E9A42B02-D5DF-448D-AA00-03F14749EB61" => "Desempenho máximo, para computadores de mesa potentes. Consome mais energia.",
        _ => "Plano criado pelo fabricante ou por você.",
    };
}

/// <summary>Lists and activates power plans (Windows power API; no admin rights needed).</summary>
public interface IPowerPlanService
{
    Task<IReadOnlyList<PowerPlan>> GetPlansAsync(CancellationToken cancellationToken = default);

    Task<bool> ActivateAsync(Guid planId, CancellationToken cancellationToken = default);
}
