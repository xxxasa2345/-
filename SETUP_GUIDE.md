# Saqer Accounting System - دليل التثبيت والبدء

## 📋 المتطلبات
- .NET 8 SDK أو أحدث
- SQL Server LocalDB أو SQL Server Express
- Visual Studio 2022 أو VS Code

## 🚀 خطوات الاستخدام

### 1️⃣ استنساخ المستودع
```bash
git clone https://github.com/a4032255565-stack/a.git
cd a
git checkout develop
```

### 2️⃣ استعادة حزم NuGet
```bash
dotnet restore
```

### 3️⃣ إنشاء قاعدة البيانات
```bash
dotnet ef database update --project src/SaqerAccountingSystem.Infrastructure --startup-project src/SaqerAccountingSystem.API
```

### 4️⃣ تشغيل التطبيق
```bash
cd src/SaqerAccountingSystem.API
dotnet run
```

### 5️⃣ فتح Swagger
افتح المتصفح وانتقل إلى:
```
https://localhost:5001/swagger/index.html
```

## 📡 API Endpoints المتاحة

### الشركات
```
GET    /api/companies           - عرض جميع الشركات
GET    /api/companies/{id}      - عرض شركة محددة
POST   /api/companies           - إضافة شركة جديدة
```

### العملاء
```
GET    /api/customers           - عرض العملاء
GET    /api/customers/{id}      - عرض عميل محدد
POST   /api/customers           - إضافة عميل جديد
```

### الموردين
```
GET    /api/suppliers           - عرض الموردين
GET    /api/suppliers/{id}      - عرض مورد محدد
POST   /api/suppliers           - إضافة مورد جديد
```

### الأصناف
```
GET    /api/items               - عرض الأصناف
GET    /api/items/{id}          - عرض صنف محدد
POST   /api/items               - إضافة صنف جديد
```

### الفروع
```
GET    /api/branches            - عرض الفروع
GET    /api/branches/{id}       - عرض فرع محدد
POST   /api/branches            - إضافة فرع جدي��
```

### المبيعات
```
GET    /api/sales               - عرض فواتير المبيعات
GET    /api/sales/{id}          - عرض فاتورة محددة
POST   /api/sales               - إنشاء فاتورة مبيعات
```

### المشتريات
```
GET    /api/purchases           - عرض فواتير المشتريات
GET    /api/purchases/{id}      - عرض فاتورة محددة
POST   /api/purchases           - إنشاء فاتورة مشتريات
```

### المخزون
```
GET    /api/inventory           - عرض حركات المخزون
GET    /api/inventory/item/{id} - حركات صنف محدد
GET    /api/inventory/stock/{itemId}/{branchId} - كمية المخزون
POST   /api/inventory           - إضافة حركة مخزون
```

### الحسابات
```
GET    /api/accounts            - عرض الحسابات
GET    /api/accounts/{id}       - عرض حساب محدد
POST   /api/accounts            - إضافة حساب جديد
```

### دفتر المعاملات
```
GET    /api/transactionjournals          - عرض المعاملات
GET    /api/transactionjournals/{id}    - عرض معاملة محددة
POST   /api/transactionjournals         - إنشاء معاملة
PUT    /api/transactionjournals/{id}/approve - الموافقة على معاملة
```

### أرصدة الحسابات
```
GET    /api/accountbalances/{accountId}  - رصيد حساب محدد
```

### التقارير المالية
```
GET    /api/financialreports             - عرض التقارير
POST   /api/financialreports/income-statement  - تقرير الدخل
POST   /api/financialreports/balance-sheet    - الميزانية العمومية
```

### لوحة التحكم
```
GET    /api/reports/dashboard   - ملخص النظام
```

## 📝 أمثلة على الاستخدام

### إضافة شركة
```json
POST /api/companies
{
  "name": "شركة صقر للمحاسبة",
  "displayName": "Saqer Accounting Co.",
  "phone": "+966501234567",
  "email": "info@saqer.sa",
  "taxNumber": "3103001234567890",
  "address": "الرياض، المملكة العربية السعودية",
  "currency": "SAR"
}
```

### إضافة عميل
```json
POST /api/customers
{
  "name": "العميل الأول",
  "phone": "+966501234567",
  "email": "customer@example.com",
  "taxNumber": "3103001234567890",
  "address": "الرياض",
  "isActive": true
}
```

### إضافة صنف
```json
POST /api/items
{
  "code": "ITEM001",
  "name": "المنتج الأول",
  "description": "وصف المنتج",
  "purchasePrice": 100,
  "salePrice": 150,
  "stockQuantity": 50,
  "companyId": 1
}
```

### إنشاء فاتورة مبيعات
```json
POST /api/sales
{
  "invoiceNumber": "SL-0001",
  "invoiceDate": "2026-10-04",
  "customerId": 1,
  "discount": 0,
  "taxAmount": 0,
  "totalAmount": 150,
  "netAmount": 150,
  "isPaid": false,
  "status": "Draft",
  "lines": [
    {
      "itemId": 1,
      "quantity": 1,
      "unitPrice": 150,
      "total": 150,
      "description": "المنتج الأول"
    }
  ]
}
```

## 🏗️ بنية المشروع

```
SaqerAccountingSystem/
├── src/
│   ├── SaqerAccountingSystem.Domain/
│   │   └── Entities/              # كيانات الأعمال
│   │
│   ├── SaqerAccountingSystem.Application/
│   │   ├── Interfaces/            # واجهات الخدمات
│   │   └── Services/              # تنفيذ الخدمات
│   │
│   ├── SaqerAccountingSystem.Infrastructure/
│   │   └── Data/                  # DbContext وقاعدة البيانات
│   │
│   └── SaqerAccountingSystem.API/
│       ├── Controllers/           # API Controllers
│       ├── Program.cs             # نقطة البداية
│       └── appsettings.json       # إعدادات الاتصال
│
└── SETUP_GUIDE.md                 # هذا الملف
```

## 🔑 الميزات الرئيسية

✅ **إدارة الشركات والفروع**
✅ **إدارة العملاء والموردين**
✅ **إدارة الأصناف والمخزون**
✅ **فواتير المبيعات والمشتريات**
✅ **دفتر المعاملات المحاسبية**
✅ **حساب الأرصدة تلقائيًا**
✅ **التقارير المالية**
✅ **لوحة تحكم ملخصة**
✅ **API كاملة عبر Swagger**
✅ **قاعدة بيانات SQL Server**

## 📊 الكيانات الأساسية

1. **Company** - الشركات
2. **Branch** - الفروع
3. **Customer** - العملاء
4. **Supplier** - الموردين
5. **Item** - الأصناف
6. **Account** - الحسابات
7. **SaleInvoice** - فواتير المبيعات
8. **PurchaseInvoice** - فواتير المشتريات
9. **TransactionJournal** - دفتر المعاملات
10. **InventoryMovement** - حركات المخزون
11. **Payment** - الدفعات
12. **FinancialReport** - التقارير المالية

## ⚠️ ملاحظات مهمة

- جميع المعاملات المحاسبية يجب أن تكون متوازنة (Debit = Credit)
- الفواتير التي تمت الموافقة عليها لا يمكن تعديلها
- المخزون يتم تحديثه تلقائيًا عند إنشاء فواتير
- التقارير المالية توليدية وتعتمد على البيانات الحالية

## 🔄 التطوير المستقبلي

- [ ] واجهة مستخدم ويب (Blazor)
- [ ] نظام مصادقة متقدم
- [ ] نظام الصلاحيات والأدوار
- [ ] تصدير التقارير PDF/Excel
- [ ] التكامل مع البنوك
- [ ] نظام الإشعارات
- [ ] التحليلات المتقدمة

## 📞 الدعم والمساهمة

مرحبًا بالمساهمات! يرجى:
1. فتح issue لأي اقتراح أو مشكلة
2. إنشاء pull request للمساهمات
3. اتبع معايير الكود الموجودة

## 📄 الترخيص

MIT License - انظر LICENSE.md للتفاصيل

---

✨ **شكرًا لاستخدام نظام صقر للمحاسبة!**
