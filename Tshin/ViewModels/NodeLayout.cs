namespace Tshin.ViewModels;

/// <summary>
/// Fixed geometry of a node card, shared between the analytic wire math here and
/// the card layout in EditorView.axaml. These constants MUST match the heights used
/// in the node DataTemplate, otherwise pins and wires drift apart.
/// </summary>
public static class NodeLayout
{
    // ── Infinite-canvas coordinate bias ──────────────────────────────────────
    // Avalonia's Canvas (used as the ItemsPanel) culls children whose LAYOUT
    // position falls outside the Canvas's own bounds — regardless of ClipToBounds.
    // So world coordinates (which can be negative or very large as cards are dragged)
    // are mapped into a large, positive canvas by adding CanvasBias. World origin
    // therefore sits at the CENTRE of the canvas, giving ±CanvasBias of room in every
    // direction. The render transform subtracts CanvasBias*Zoom so the view is
    // unchanged; all pointer/world math stays in unbiased world space.
    public const double CanvasBias = 100_000;
    public const double CanvasSize = 2 * CanvasBias;

    public const double Width = 240;
    public const double HeaderHeight = 34;
    public const double TextAreaHeight = 64;
    // Row pitch per choice. The visible block is smaller than this; the slack is the
    // gap between blocks. Output pins sit at the row's vertical centre.
    public const double ChoiceRowHeight = 40;
    public const double PinRadius = 6;

    /// <summary>Y (relative to node top) where the first choice row begins.</summary>
    public const double ChoicesTop = HeaderHeight + TextAreaHeight;

    public static double OutputPinX(NodeViewModel n) => n.X + Width;

    public static double OutputPinY(NodeViewModel n, int choiceIndex)
        => n.Y + ChoicesTop + choiceIndex * ChoiceRowHeight + ChoiceRowHeight / 2;

    public static double InputPinX(NodeViewModel n) => n.X;

    public static double InputPinY(NodeViewModel n) => n.Y + HeaderHeight / 2;
}
