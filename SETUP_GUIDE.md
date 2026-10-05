# دليل تشغيل نظام صقر للمحاسبة

## 1. المستودع الصحيح

المستودع المعتمد هو:

```
https://github.com/xxxasa2345/-.git
```

لا تستخدم مستودع `AlSaqarAccountingV4` لهذا الإصدار.

## 2. المتطلبات

- Windows 10/11
- .NET 8 SDK
- SQL Server LocalDB أو SQL Server Express
- Visual Studio 2022 أو VS Code

تحقق من .NET:

```powershell
dotnet --version
```

تحقق من LocalDB:

```powershell
sqllocaldb info
```

## 3. تنزيل المشروع

```powershell
git clone https://github.com/xxxasa2345/-.git
cd -
```

## 4. بناء المشروع

```powershell
dotnet restore SaqerAccountingSystem.sln
dotnet build SaqerAccountingSystem.sln --configuration Release
```

## 5. تشغيل النظام

شغل الـAPI أولاً:

```powershell
dotnet run --project src/SaqerAccountingSystem.API
```

العنوان الافتراضي:

```
http://localhost:5000
```

Swagger:

```
http://localhost:5000/swagger
```

ثم في نافذة PowerShell ثانية شغل التطبيق:

```powershell
dotnet run --project src/SaqerAccountingSystem.Desktop
```

## 6. إعداد الاتصال

API:

```
src/SaqerAccountingSystem.API/appsettings.json
```

سطح المكتب:

```
src/SaqerAccountingSystem.Desktop/appsettings.json
```

يمكن تغيير عنوان API بدون تعديل الكود:

```powershell
$env:SAQER_API_URL="http://localhost:5000"
```

## 7. قاعدة البيانات

النظام يستخدم:

```
Server=(localdb)\\MSSQLLocalDB;Database=SaqerAccountingSystem;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True
```

عند أول تشغيل يقوم النظام بإنشاء قاعدة البيانات والجداول والبيانات الأساسية.

## 8. الدخول الأول

```
admin / admin123
accountant / 123456
```

بعد الدخول غيّر كلمات المرور وأنشئ المستخدمين الفعليين من إدارة النظام.

## 9. تسلسل التشغيل المحاسبي

```
تسجيل الدخول
  ↓
المجموعة
  ↓
الصلاحيات
  ↓
الشاشة
  ↓
العملية
  ↓
المستند
  ↓
الترحيل
  ↓
القيد
  ↓
الأستاذ العام
  ↓
التقارير
```

## 10. أهم العمليات

### قيد يومية
```
POST /api/journals
POST /api/journals/{id}/post
```

يشترط توازن المدين والدائن.

### فاتورة مبيعات
```
POST /api/sales
POST /api/sales/{id}/post
```

الترحيل يحدث المخزون ويولّد القيد المحاسبي.

### فاتورة مشتريات
```
POST /api/purchases
POST /api/purchases/{id}/post
```

الترحيل يزيد المخزون ويولّد القيد.

### دفعة
```
POST /api/payments
```

تنتج قيدًا محاسبيًا على الصندوق/البنك مقابل حساب العميل أو المورد.

## 11. التقارير

```
GET /api/reports/trial-balance
GET /api/reports/income-statement
GET /api/reports/balance-sheet
```

التقارير تعتمد على القيود المرحّلة في قاعدة البيانات.

## 12. تشغيل تطبيق سطح المكتب من الحل

```powershell
dotnet run --project src/SaqerAccountingSystem.Desktop
```

تطبيق سطح المكتب يقرأ البيانات من نفس API، لذلك لا توجد قاعدة بيانات ثانية مخفية داخل التطبيق.

## 13. النسخ الاحتياطي

في الإنتاج استخدم SQL Server Backup الفعلي. لا تعتمد على ملفات المشروع أو ملفات `.db` كنسخة احتياطية.

## 14. ملاحظات قبل الإنتاج

يجب تخصيص شجرة الحسابات، الضرائب، الفترات المالية، سياسات الإهلاك، صلاحيات المستخدمين، ومتطلبات الفوترة الإلكترونية حسب طبيعة المنشأة قبل استخدام النظام على بيانات مالية فعلية.
