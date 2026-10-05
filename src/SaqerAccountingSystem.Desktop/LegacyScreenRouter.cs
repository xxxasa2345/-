namespace SaqerAccountingSystem.Desktop;

public static class LegacyScreenRouter
{
    public static string ResolveModule(string? screenName)
    {
        var n = (screenName ?? "").Trim().ToLowerInvariant();
        if (Contains(n, "لوحة", "الرئيسية", "dashboard", "main")) return "dashboard";
        if (Contains(n, "شركة", "شركات", "فرع", "فروع", "company", "branch", "بيانات المنشأة")) return "companies";
        if (Contains(n, "عميل", "عملاء", "customer", "ذمم العملاء")) return "customers";
        if (Contains(n, "مورد", "موردين", "مورّد", "supplier", "دائن")) return "suppliers";
        if (Contains(n, "صنف", "أصناف", "مادة", "مواد", "item", "product")) return "items";
        if (Contains(n, "مبيع", "مبيعات", "فاتورة بيع", "sales", "sale")) return "sales";
        if (Contains(n, "شراء", "مشتريات", "فاتورة شراء", "purchase")) return "purchases";
        if (Contains(n, "حساب", "الحسابات", "دليل", "account", "chart", "أستاذ")) return "accounts";
        if (Contains(n, "قيد", "قيود", "يومية", "journal", "ledger")) return "journals";
        if (Contains(n, "صندوق", "بنك", "قبض", "صرف", "سداد", "payment", "cash", "bank")) return "payments";
        if (Contains(n, "مخزون", "مستودع", "مخازن", "حركة مخزون", "inventory", "stock", "warehouse")) return "inventory";
        if (Contains(n, "ضريبة", "ضريبي", "vat", "tax")) return "tax";
        if (Contains(n, "أصل ثابت", "أصول ثابتة", "fixed asset", "asset")) return "assets";
        if (Contains(n, "مركز تكلفة", "مراكز التكلفة", "cost center", "costcenter")) return "costcenters";
        if (Contains(n, "موازنة", "موازنات", "ميزانية تقديرية", "budget")) return "budgets";
        if (Contains(n, "تقرير", "تقارير", "ميزان مراجعة", "قائمة دخل", "balance sheet", "income statement", "report")) return "reports";
        if (Contains(n, "مستخدم", "مستخدمين", "صلاحيات", "أمن", "أمان", "user", "permission", "security")) return "users";
        if (Contains(n, "إعداد", "اعداد", "settings", "system")) return "settings";
        return "";
    }

    private static bool Contains(string value, params string[] terms)
        => terms.Any(value.Contains);
}
