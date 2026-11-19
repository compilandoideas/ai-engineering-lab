using HitlSendReply.Lab.Fakes;
using HitlSendReply.Policy;
using HitlSendReply.Tickets;

namespace HitlSendReply.Tests.Policy;

public class FactCheckerSpanishTests
{
    private static readonly Order Shipped = SampleTickets.Get("T-1001", Language.Es).Order;     // #A-1042, 12/11 y 21/11, 129,90
    private static readonly Order Processing = SampleTickets.Get("T-1003", Language.Es).Order;  // #A-1103, ETA 28/11, 1.500,00

    [Theory]
    [InlineData("Según nuestro sistema, su pedido #A-1042 se despachó el 12 de noviembre.")]
    [InlineData("El pedido #A-1042 fue despachado el 12 de noviembre de 2026 y llegará el 21 de noviembre.")]
    [InlineData("Se despachó el 12/11.")]
    [InlineData("El total de su pedido fue de $129,90.")]
    [InlineData("El total de su pedido fue de 129,90 USD.")]
    [InlineData("Gracias por su mensaje, quedó registrado en su ticket.")]
    public void Verifica_cuando_cada_dato_coincide(string text) =>
        Assert.True(FactChecker.Check(text, Shipped, Language.Es).Verified);

    [Theory]
    [InlineData("El total de su pedido es de $1.500.")]
    [InlineData("El total de su pedido es de $1.500,00.")]
    [InlineData("Su pedido #A-1103 está en preparación; llegará el 28 de noviembre.")]
    public void Entiende_separador_de_miles_y_estados_en_espanol(string text) =>
        Assert.True(FactChecker.Check(text, Processing, Language.Es).Verified);

    [Theory]
    [InlineData("Su pedido #A-1042 se despachó el 14 de noviembre.", "does not match any date")]
    [InlineData("Su pedido #A-9999 se despachó el 12 de noviembre.", "does not match the ticket's order")]
    [InlineData("Le reembolsaremos $500.", "has no source")]
    [InlineData("Le reembolsaremos $1.500.", "has no source")] // 1500 ≠ 129,90
    [InlineData("Su pedido llegará mañana.", "cannot be verified")]
    [InlineData("Le responderemos hoy.", "cannot be verified")]
    [InlineData("Llegará dentro de tres días hábiles.", "cannot be verified")]
    [InlineData("Llegará en dos días.", "cannot be verified")]
    [InlineData("Llegará en 3 días.", "Unverifiable number")]
    [InlineData("Su pedido fue entregado.", "contradicts the record")]
    [InlineData("Se despachó el 31 de febrero.", "not a valid date")]
    public void Escala_lo_que_no_puede_verificar(string text, string expectedIssue)
    {
        var result = FactChecker.Check(text, Shipped, Language.Es);

        Assert.False(result.Verified);
        Assert.Contains(result.Issues, i => i.Contains(expectedIssue, StringComparison.Ordinal));
    }

    [Fact]
    public void El_monto_en_ingles_no_se_lee_con_reglas_espanolas()
    {
        // "$1,500" in a Spanish ticket means 1.5: it does not match 1500 and escalates. Conservative by design.
        Assert.False(FactChecker.Check("El total es de $1,500.", Processing, Language.Es).Verified);
    }
}
