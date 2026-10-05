# تشغيل نظام صقر للمحاسبة

## المتطلبات
- .NET 8 SDK
- Visual Studio 2022 أو VS Code

## التشغيل

```bash
git clone https://github.com/xxxasa2345/-.git
cd -
dotnet restore
dotnet run --project src/SaqerAccountingSystem.API
```

بعد التشغيل افتح عنوان HTTPS الذي يظهر في الطرفية.

Swagger:
```
https://localhost:5001/swagger
```

الواجهة:
```
https://localhost:5001/
```

## الحسابات التجريبية

مدير النظام:
`admin / admin123`

المحاسب:
`accountant / 123456`

## تدفق الصلاحيات

`Login → Group → Permission → Screen → Operation`

كل مستخدم يحصل على مجموعة وصلاحيات، والقائمة الجانبية والشاشات المتاحة تتغير حسب الصلاحيات.

## ملاحظة

النسخة الحالية تستخدم بيانات تجريبية داخل الذاكرة لتثبيت الواجهة وتدفق الصلاحيات. المرحلة التالية هي ربطها بـ EF Core وSQL Server وتحويل العمليات إلى CRUD ومعاملات محاسبية فعلية.
