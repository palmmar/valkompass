namespace Valkompass.Domain.Enums;

/// <summary>Vilken av Valmyndighetens räkningar siffrorna kommer från.</summary>
public enum CountingStage
{
    /// <summary>Preliminär räkning: valnatten och onsdagens uppsamlingsräkning.</summary>
    Preliminary = 0,

    /// <summary>Slutlig räkning: länsstyrelsernas kontrollräkning veckan efter valet.</summary>
    Final = 1,
}
