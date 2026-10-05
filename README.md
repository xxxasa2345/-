# نظام صقر للمحاسبة — Saqer Accounting System

نظام محاسبي وERP مبني على **.NET 8 + ASP.NET Core + SQL Server + Entity Framework Core + Windows Forms**. تم بناء الطبقات على الملفات الموجودة في المستودع دون حذفها، مع تحويل مصدر البيانات والعمليات المحاسبية الأساسية من بيانات تجريبية إلى قاعدة بيانات فعلية.

## البنية

```
SaqerAccountingSystem.sln
├── Domain
│   └── الكيانات المحاسبية والمالية
├── Application
│   └── قواعد المحاسبة والتحقق من توازن القيود
├── Infrastructure
│   └── EF Core / SQL Server / التهيئة والصلاحيات
├── API
│   └── REST API + Swagger + واجهة النظام
└── Desktop
    └── تطبيق Windows Forms متصل بالـAPI
```

## الوحدات المحاسبية

- الشركات والفروع
- دليل الحسابات والأستاذ العام
- القيود اليومية والترحيل
- العملاء والذمم المدينة
- الموردون والذمم الدائنة
- المبيعات
- المشتريات
- الخزينة والبنوك والدفعات
- الأصناف والمخزون وحركاته
- ضريبة القيمة المضافة
- الأصول الثابتة
- مراكز التكلفة
- الموازنات
- التقارير المالية: ميزان المراجعة، قائمة الدخل، الميزانية العمومية
- المستخدمون والمجموعات والصلاحيات
- سجل التدقيق Audit Log
- لوحة تحكم تشغيلية

## دورة المستند المحاسبي

المبدأ المستخدم هو:

**Login → Group → Permission → Screen → Operation → Journal → Ledger/Reports**

الفاتورة تحفظ كمستند، وعند الترحيل يتم إنشاء قيد محاسبي متوازن، وتحديث المخزون ضمن المعاملة نفسها.

## الحسابات الابتدائية

يتم إنشاء دليل أولي عند أول تشغيل، ويتضمن حسابات رئيسية مثل:

- 1101 الصندوق
- 1102 البنوك
- 1201 العملاء
- 1301 المخزون
- 2101 الموردون
- 2201 ضريبة القيمة المضافة
- 3101 رأس المال
- 4101 إيرادات المبيعات
- 5101 تكلفة المبيعات
- 5201 المصروفات التشغيلية

## الوصول الأولي

الحسابات الأولية تُنشأ في قاعدة البيانات عند أول تشغيل فقط:

- `admin / admin123`
- `accountant / 123456`

هذه بيانات تهيئة وليست بيانات مخزنة في الواجهة، ويُنصح بتغيير كلمات المرور مباشرة بعد أول دخول.

## التشغيل السريع على Windows

يتطلب:
- .NET 8 SDK
- SQL Server LocalDB أو SQL Server Express
- Visual Studio 2022 أو VS Code

### تنزيل المشروع

```powershell
git clone https://github.com/xxxasa2345/-.git
cd -
```

### تشغيل الـAPI

```powershell
dotnet restore SaqerAccountingSystem.sln
dotnet run --project src/SaqerAccountingSystem.API
```

ثم افتح:

```
http://localhost:5000
```

وSwagger:

```
http://localhost:5000/swagger
```

### تشغيل تطبيق سطح المكتب

في نافذة PowerShell ثانية:

```powershell
dotnet run --project src/SaqerAccountingSystem.Desktop
```

تطبيق سطح المكتب يتصل بـAPI من خلال:

```
src/SaqerAccountingSystem.Desktop/appsettings.json
```

ويمكن تغيير العنوان باستخدام متغير البيئة:

```powershell
$env:SAQER_API_URL="http://localhost:5000"
```

## قاعدة البيانات

الاتصال الافتراضي موجود في:

```
src/SaqerAccountingSystem.API/appsettings.json
```

ويستخدم SQL Server LocalDB:

```
Server=(localdb)\\MSSQLLocalDB;Database=SaqerAccountingSystem;Trusted_Connection=True;TrustServerCertificate=True
```

عند أول تشغيل يستخدم النظام `EnsureCreated` لإنشاء الجداول والبيانات الأساسية.

لبيئة إنتاج فعلية يوصى باستخدام SQL Server Express/Standard مع حساب قاعدة بيانات مخصص، نسخ احتياطية دورية، ومigrations رسمية قبل ترقية المخطط.

## قواعد محاسبية مطبقة

- القيد لا يُرحل إذا لم يكن المدين مساويًا للدائن.
- لا يسمح بالمخزون السالب في الحركات اليدوية أو ترحيل المبيعات.
- ترحيل المبيعات يحدث داخله قيد الإيراد/الضريبة/العملاء أو الصندوق وتكلفة المبيعات/المخزون.
- ترحيل المشتريات يحدث داخله قيد المخزون/الضريبة مقابل المورد أو الصندوق.
- عمليات الدفعات تولد قيودًا محاسبية.
- كل عملية إنشاء أو ترحيل رئيسية تسجل في Audit Log.

## API الرئيسية

```
POST /api/auth/login
POST /api/auth/logout
GET  /api/auth/me
GET  /api/navigation

GET/POST /api/companies
GET/POST /api/branches
GET/POST /api/customers
GET/POST /api/suppliers
GET/POST /api/items
GET/POST /api/accounts

GET/POST /api/journals
POST /api/journals/{id}/post

GET/POST /api/sales
POST /api/sales/{id}/post

GET/POST /api/purchases
POST /api/purchases/{id}/post

GET/POST /api/inventory
GET/POST /api/payments
GET/POST /api/tax
GET/POST /api/assets
GET/POST /api/cost-centers
GET/POST /api/budgets

GET /api/reports/trial-balance
GET /api/reports/income-statement
GET /api/reports/balance-sheet
GET /api/dashboard

GET/POST /api/admin/users
GET /api/admin/groups
GET /api/audit
```

## ملاحظات إنتاجية

المستودع الآن يحتوي على طبقة بيانات حقيقية، لكن اعتماد نظام محاسبي إنتاجي كامل يحتاج قبل التشغيل الفعلي للشركة إلى مراجعة محاسب/مدقق، إعداد شجرة الحسابات حسب نشاط المنشأة، سياسات إغلاق الفترات، النسخ الاحتياطي، صلاحيات أدق على مستوى الشركة والفرع، ومتطلبات الفوترة الإلكترونية والتكاملات التي تختارها المنشأة.

