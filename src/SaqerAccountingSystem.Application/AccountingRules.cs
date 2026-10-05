namespace SaqerAccountingSystem.Application;

public static class AccountingRules
{
    public static void ValidateBalanced(decimal debit, decimal credit)
    {
        if (debit < 0 || credit < 0)
            throw new InvalidOperationException("لا يسمح بقيم سالبة في طرفي القيد.");

        if (decimal.Round(debit, 2) != decimal.Round(credit, 2))
            throw new InvalidOperationException("القيد المحاسبي غير متوازن: يجب أن يساوي المدين الدائن.");
    }

    public static decimal CalculateTax(decimal taxableAmount, decimal rate)
        => decimal.Round(taxableAmount * rate / 100m, 2, MidpointRounding.AwayFromZero);
}
