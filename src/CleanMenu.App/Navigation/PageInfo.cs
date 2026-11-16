namespace CleanMenu.App.Navigation;

/// <summary>
/// Static description of a section, written for non-technical users.
/// </summary>
/// <param name="Page">Section id.</param>
/// <param name="Title">Name shown in the sidebar and as page title.</param>
/// <param name="Description">One plain sentence saying what the section is for.</param>
/// <param name="Glyph">Icon code point (see <see cref="CleanMenu.UI.Glyphs"/>).</param>
/// <param name="Group">Sidebar group label.</param>
/// <param name="IsAdvanced">True for areas meant for experienced users; shown with an "Avançado" tag.</param>
/// <param name="PlannedFeatures">What the section will let the user do, in plain words.</param>
/// <param name="PlannedStage">Development stage in which the section becomes functional.</param>
public sealed record PageInfo(
    AppPage Page,
    string Title,
    string Description,
    string Glyph,
    string Group,
    bool IsAdvanced,
    IReadOnlyList<string> PlannedFeatures,
    int PlannedStage);
