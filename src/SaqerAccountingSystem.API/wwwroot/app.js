const state = {
  token: localStorage.getItem("saqer_token"),
  user: null,
  permissions: [],
  navigation: [],
  company: null,
  branches: [],
  module: "dashboard"
};

const labels = {
  dashboard:"لوحة التحكم", companies:"الشركات والفروع", customers:"العملاء والذمم", suppliers:"الموردون والدائنون",
  items:"الأصناف", sales:"المبيعات", purchases:"المشتريات", accounts:"دليل الحسابات", journals:"القيود والأستاذ العام",
  payments:"الخزينة والبنوك", inventory:"المخزون", tax:"الضريبة", assets:"الأصول الثابتة", costcenters:"مراكز التكلفة",
  budgets:"الموازنات", reports:"التقارير المالية", settings:"الإعدادات"
};
const icons = {
  dashboard:"▦",companies:"⌂",customers:"◉",suppliers:"◎",items:"▤",sales:"▣",purchases:"▥",accounts:"◫",
  journals:"≡",payments:"◌",inventory:"◈",tax:"٪",assets:"▥",costcenters:"◇",budgets:"◫",reports:"◰",settings:"⚙"
};

function esc(value) {
  return String(value ?? "").replaceAll("&","&amp;").replaceAll("<","&lt;").replaceAll(">","&gt;").replaceAll('"',"&quot;");
}
function can(permission) {
  return state.permissions.some(x => x.toLowerCase() === permission.toLowerCase());
}
function companyId() { return state.company?.id || 1; }
function branchId() { return state.branches[0]?.id || 1; }

async function api(url, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (state.token) headers.Authorization = `Bearer ${state.token}`;
  if (options.body) headers["Content-Type"] = "application/json";
  const response = await fetch(url, { ...options, headers });
  if (response.status === 401) {
    state.token = null; localStorage.removeItem("saqer_token"); renderLogin(); throw new Error("انتهت الجلسة");
  }
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.message || "تعذر تنفيذ الطلب");
  return data;
}

function renderLogin(error = "") {
  document.getElementById("app").innerHTML = `
    <main class="auth-page">
      <section class="auth-panel"><div class="logo-mark">ص</div><div><div class="eyebrow">SAQER ERP</div>
      <h1>نظام صقر للمحاسبة</h1><p>نظام محاسبي فعلي يعمل بقاعدة بيانات SQL Server ويعالج المستندات والحسابات والمخزون والتقارير.</p></div>
      <div class="feature-row"><span>الأستاذ العام</span><span>الذمم</span><span>المخزون</span><span>الضريبة</span><span>التقارير</span></div></section>
      <section class="auth-card"><div class="card-title">تسجيل الدخول</div><div class="muted">بيانات البداية تنشأ في قاعدة البيانات عند أول تشغيل.</div>
      <form id="loginForm" class="form-grid"><label>اسم المستخدم<input id="username" value="admin" autocomplete="username"></label>
      <label>كلمة المرور<input id="password" type="password" value="admin123" autocomplete="current-password"></label>
      <button class="primary" type="submit">دخول النظام</button></form>
      ${error ? `<div class="error">${esc(error)}</div>` : ""}</section>
    </main>`;
  document.getElementById("loginForm").addEventListener("submit", async e => {
    e.preventDefault();
    try {
      const data = await api("/api/auth/login",{method:"POST",body:JSON.stringify({
        username:document.getElementById("username").value.trim(),
        password:document.getElementById("password").value
      })});
      state.token=data.token; state.user=data.user; state.permissions=data.permissions||[];
      state.navigation=data.navigation||[]; localStorage.setItem("saqer_token",state.token);
      renderShell(); await loadContext(); await loadModule("dashboard");
    } catch(error) { renderLogin(error.message || "فشل تسجيل الدخول."); }
  });
}

function renderShell() {
  document.getElementById("app").innerHTML = `
    <div class="shell"><aside class="sidebar">
      <div class="brand"><div class="logo-mark small">ص</div><div><strong>صقر</strong><span>ERP</span></div></div>
      <div class="sidebar-user"><div class="avatar">${esc((state.user?.fullName||"م").slice(0,1))}</div>
      <div><strong>${esc(state.user?.fullName)}</strong><small>${esc(state.user?.Username||state.user?.username||"")}</small></div></div>
      <nav id="nav"></nav><button class="logout" id="logoutBtn">تسجيل الخروج</button>
    </aside><main class="main"><header class="topbar"><div><div class="eyebrow">نظام صقر للمحاسبة</div>
      <h2 id="pageTitle">لوحة التحكم</h2></div><div class="top-actions"><span class="status-dot">متصل</span><span class="date-chip" id="companyChip">SQL Server</span></div>
    </header><section id="content" class="content"></section></main></div>`;
  document.getElementById("logoutBtn").onclick=async()=>{ try{await api("/api/auth/logout",{method:"POST"});}catch{} state.token=null; localStorage.removeItem("saqer_token"); renderLogin(); };
  buildNav();
}
async function loadContext() {
  const me=await api("/api/auth/me");
  state.company=me.company; state.branches=me.branches||[];
  document.getElementById("companyChip").textContent=me.company ? me.company.name : "SQL Server";
}
function buildNav() {
  const nav=document.getElementById("nav");
  nav.innerHTML=(state.navigation||[]).map(item=>`<button class="nav-item ${item.key===state.module?"active":""}" data-key="${esc(item.key)}">
    <span class="nav-icon">${icons[item.key]||"•"}</span><span>${esc(item.title||item.Title)}</span></button>`).join("");
  nav.querySelectorAll(".nav-item").forEach(btn=>btn.onclick=async()=>{state.module=btn.dataset.key;buildNav();await loadModule(state.module);});
}
async function loadModule(module) {
  document.getElementById("pageTitle").textContent=labels[module]||"النظام";
  const content=document.getElementById("content");
  if(module==="dashboard"){content.innerHTML='<div class="loading">تحميل اللوحة...</div>';try{content.innerHTML=dashboardView(await api("/api/dashboard"));}catch(e){content.innerHTML=errorView(e.message);}return;}
  content.innerHTML='<div class="loading">جارٍ تحميل البيانات...</div>';
  try { const data=await api("/api/modules/"+module); content.innerHTML=moduleView(module,data); } catch(e) { content.innerHTML=errorView(e.message); }
}
function errorView(message){return `<section class="panel"><div class="error">${esc(message)}</div></section>`;}
function dashboardView(data){
 return `<div class="hero"><div><div class="eyebrow">نظام فعلي</div><h3>مرحبًا ${esc(state.user?.fullName)} 👋</h3>
 <p>المستندات المرحّلة تُنتج قيودًا محاسبية وتحدث المخزون ضمن قاعدة البيانات.</p></div><div class="hero-badge">Login → Group → Permission → Screen → Operation</div></div>
 <div class="cards">${data.cards.map(c=>`<article class="metric"><div class="metric-top"><span>${esc(c.title)}</span><span class="metric-icon">${icons[c.key]||"◌"}</span></div>
 <strong>${Number(c.value).toLocaleString("ar-SA")} ر.س</strong><small>من قاعدة البيانات</small></article>`).join("")}</div>
 <div class="grid-two"><section class="panel"><div class="panel-head"><h4>آخر الفواتير</h4><span>SQL Server</span></div>
 <div class="table-wrap"><table><thead><tr><th>الرقم</th><th>الطرف</th><th>التاريخ</th><th>المبلغ</th><th>الحالة</th></tr></thead>
 <tbody>${data.recentInvoices.map(x=>`<tr><td>${esc(x.number)}</td><td>${esc(x.party)}</td><td>${esc(x.date)}</td><td>${Number(x.amount).toLocaleString("ar-SA")} ر.س</td><td><span class="pill">${esc(x.status)}</span></td></tr>`).join("")}</tbody></table></div></section>
 <section class="panel"><div class="panel-head"><h4>سجل التدقيق</h4><span>آخر العمليات</span></div><div class="activity">
 ${data.activity.map(x=>`<div class="activity-row"><span class="activity-time">${esc(x.time)}</span><span class="activity-dot"></span><div>${esc(x.text)}</div></div>`).join("")}</div></section></div>`;
}

function moduleView(module,data){
  if(module==="settings") return settingsView();
  if(module==="reports") return reportsView();
  const rows=Array.isArray(data)?data:[];
  const columns=rows.length?Object.keys(rows[0]):[];
  const createPermission=createPerm(module);
  return `<section class="panel"><div class="panel-head"><div><h4>${labels[module]}</h4><span>${rows.length} سجل</span></div>
  ${can(createPermission)?`<button class="primary" onclick="showCreate('${module}')">إضافة جديد</button>`:""}</div>
  ${moduleActions(module,rows)}
  <div id="createArea"></div><div class="table-wrap"><table><thead><tr>${columns.map(c=>`<th>${esc(c)}</th>`).join("")}${module==="sales"||module==="purchases"?"<th>إجراء</th>":""}</tr></thead>
  <tbody>${rows.map(row=>`<tr>${columns.map(c=>`<td>${esc(formatCell(row[c]))}</td>`).join("")}${invoiceAction(module,row)}</tr>`).join("")}</tbody></table></div></section>`;
}
function formatCell(v){ if(v===null||v===undefined)return ""; if(typeof v==="object")return JSON.stringify(v); return v; }
function createPerm(m){const map={companies:"companies.create",customers:"customers.create",suppliers:"suppliers.create",items:"items.create",accounts:"accounts.create",sales:"sales.create",purchases:"purchases.create",journals:"journals.create",inventory:"inventory.create",payments:"payments.create",tax:"tax.create",assets:"assets.create",costcenters:"costcenters.create",budgets:"budgets.create"};return map[m]||"";
}
function invoiceAction(module,row){
 if((module!=="sales"&&module!=="purchases")||!row.status||String(row.status).toLowerCase()!=="draft"||!can(module+".post"))return "";
 return `<td><button class="secondary" onclick="postInvoice('${module}',${row.id})">ترحيل</button></td>`;
}
function moduleActions(module){return "";}

function showCreate(module){
 const area=document.getElementById("createArea"); if(!area)return;
 const now=new Date().toISOString().slice(0,10);
 const simple={
 companies:`<label>الرمز<input id="f_code"></label><label>الاسم<input id="f_name"></label><label>الرقم الضريبي<input id="f_tax"></label>`,
 customers:`<label>الكود<input id="f_code"></label><label>الاسم<input id="f_name"></label><label>الهاتف<input id="f_phone"></label><label>البريد<input id="f_email"></label>`,
 suppliers:`<label>الكود<input id="f_code"></label><label>الاسم<input id="f_name"></label><label>الهاتف<input id="f_phone"></label><label>البريد<input id="f_email"></label>`,
 items:`<label>الكود<input id="f_code"></label><label>الاسم<input id="f_name"></label><label>سعر الشراء<input id="f_purchase" type="number" step="0.01"></label><label>سعر البيع<input id="f_sale" type="number" step="0.01"></label><label>الضريبة %<input id="f_taxrate" type="number" step="0.01" value="15"></label>`,
 accounts:`<label>الكود<input id="f_code"></label><label>الاسم<input id="f_name"></label><label>نوع الحساب<select id="f_type"><option>Asset</option><option>Liability</option><option>Equity</option><option>Revenue</option><option>Expense</option></select></label>`,
 inventory:`<label>الصنف ID<input id="f_item" type="number"></label><label>وارد<input id="f_in" type="number" step="0.01" value="0"></label><label>صادر<input id="f_out" type="number" step="0.01" value="0"></label><label>تكلفة الوحدة<input id="f_cost" type="number" step="0.01" value="0"></label>`,
 payments:`<label>الرقم<input id="f_number"></label><label>نوع الطرف<select id="f_party"><option value="Customer">عميل</option><option value="Supplier">مورد</option></select></label><label>حساب الصندوق/البنك ID<input id="f_cash" type="number" value="1"></label><label>المبلغ<input id="f_amount" type="number" step="0.01"></label><label>الطريقة<select id="f_method"><option>Cash</option><option>Bank</option><option>Card</option><option>Transfer</option><option>Cheque</option></select></label>`,
 tax:`<label>الكود<input id="f_code"></label><label>الاسم<input id="f_name"></label><label>النسبة %<input id="f_rate" type="number" step="0.01" value="15"></label>`,
 assets:`<label>الكود<input id="f_code"></label><label>الاسم<input id="f_name"></label><label>التكلفة<input id="f_cost" type="number" step="0.01"></label><label>العمر بالشهور<input id="f_life" type="number" value="60"></label>`,
 costcenters:`<label>الكود<input id="f_code"></label><label>الاسم<input id="f_name"></label>`,
 budgets:`<label>الحساب ID<input id="f_account" type="number"></label><label>السنة<input id="f_year" type="number" value="${new Date().getFullYear()}"></label><label>الشهر<input id="f_month" type="number" value="${new Date().getMonth()+1}"></label><label>المبلغ<input id="f_amount" type="number" step="0.01"></label>`
 };
 let body=simple[module]||"";
 if(module==="sales"||module==="purchases") body=`<label>رقم الفاتورة<input id="f_number"></label><label>التاريخ<input id="f_date" type="date" value="${now}"></label><label>ID الطرف<input id="f_partyId" type="number"></label><label>ID الصنف<input id="f_item" type="number"></label><label>الكمية<input id="f_qty" type="number" step="0.01" value="1"></label><label>السعر<input id="f_price" type="number" step="0.01"></label>`;
 if(module==="journals") body=`<label>البيان<input id="f_desc"></label><label>سطر القيد JSON<small>مثال: [{"accountId":1,"debit":100,"credit":0},{"accountId":2,"debit":0,"credit":100}]</small><textarea id="f_lines" rows="6">[]</textarea></label>`;
 area.innerHTML=`<div class="create-form"><div class="form-grid">${body}</div><div class="create-actions"><button class="primary" onclick="submitCreate('${module}')">حفظ</button><button class="secondary" onclick="document.getElementById('createArea').innerHTML=''">إلغاء</button><span id="createMessage"></span></div></div>`;
}

async function submitCreate(module){
 const message=document.getElementById("createMessage"); const get=id=>document.getElementById(id)?.value;
 try{
  let url="/api/"+module, body={};
  if(module==="companies") body={code:get("f_code"),name:get("f_name"),taxNumber:get("f_tax"),currency:"SAR"};
  else if(module==="customers") body={companyId:companyId(),code:get("f_code"),name:get("f_name"),phone:get("f_phone"),email:get("f_email")};
  else if(module==="suppliers") body={companyId:companyId(),code:get("f_code"),name:get("f_name"),phone:get("f_phone"),email:get("f_email")};
  else if(module==="items") body={companyId:companyId(),code:get("f_code"),name:get("f_name"),purchasePrice:Number(get("f_purchase")),salePrice:Number(get("f_sale")),taxRate:Number(get("f_taxrate")),inventoryAccountId:4,salesAccountId:8,costAccountId:9};
  else if(module==="accounts") body={companyId:companyId(),code:get("f_code"),name:get("f_name"),type:get("f_type"),isControlAccount:false,isActive:true};
  else if(module==="inventory") body={companyId:companyId(),branchId:branchId(),itemId:Number(get("f_item")),date:new Date().toISOString(),quantityIn:Number(get("f_in")),quantityOut:Number(get("f_out")),unitCost:Number(get("f_cost"))};
  else if(module==="payments") body={companyId:companyId(),branchId:branchId(),number:get("f_number"),date:new Date().toISOString(),partyType:get("f_party"),customerId:null,supplierId:null,cashOrBankAccountId:Number(get("f_cash")),amount:Number(get("f_amount")),method:get("f_method"),description:""};
  else if(module==="tax") body={companyId:companyId(),code:get("f_code"),name:get("f_name"),rate:Number(get("f_rate")),isSales:true,isPurchase:true};
  else if(module==="assets") body={companyId:companyId(),code:get("f_code"),name:get("f_name"),acquisitionDate:new Date().toISOString(),cost:Number(get("f_cost")),salvageValue:0,usefulLifeMonths:Number(get("f_life"))};
  else if(module==="costcenters") body={companyId:companyId(),code:get("f_code"),name:get("f_name")};
  else if(module==="budgets") body={companyId:companyId(),accountId:Number(get("f_account")),year:Number(get("f_year")),month:Number(get("f_month")),amount:Number(get("f_amount"))};
  else if(module==="sales") body={companyId:companyId(),branchId:branchId(),number:get("f_number"),date:get("f_date"),customerId:Number(get("f_partyId"))||null,lines:[{itemId:Number(get("f_item")),quantity:Number(get("f_qty")),unitPrice:Number(get("f_price"))||null,discount:0}]};
  else if(module==="purchases") body={companyId:companyId(),branchId:branchId(),number:get("f_number"),date:get("f_date"),supplierId:Number(get("f_partyId"))||null,lines:[{itemId:Number(get("f_item")),quantity:Number(get("f_qty")),unitPrice:Number(get("f_price"))||null,discount:0}]};
  else if(module==="journals") body={companyId:companyId(),branchId:branchId(),date:new Date().toISOString(),description:get("f_desc"),lines:JSON.parse(get("f_lines")||"[]")};
  await api(url,{method:"POST",body:JSON.stringify(body)});
  message.textContent="تم الحفظ في قاعدة البيانات.";
  await loadModule(module);
 }catch(e){message.textContent=e.message;message.className="error";}
}

async function postInvoice(module,id){
 try{await api(`/api/${module}/${id}/post`,{method:"POST",body:JSON.stringify({isPaid:false,cashOrBankAccountId:1})});await loadModule(module);}
 catch(e){alert(e.message);}
}
function reportsView(){return `<section class="panel"><div class="panel-head"><h4>التقارير المالية</h4><span>من الأستاذ العام</span></div>
 <div class="report-grid"><button class="secondary" onclick="report('trial-balance')">ميزان المراجعة</button><button class="secondary" onclick="report('income-statement')">قائمة الدخل</button><button class="secondary" onclick="report('balance-sheet')">الميزانية العمومية</button></div><div id="reportResult"></div></section>`;}
async function report(kind){try{const data=await api("/api/reports/"+kind);document.getElementById("reportResult").innerHTML=`<pre class="report-json">${esc(JSON.stringify(data,null,2))}</pre>`;}catch(e){document.getElementById("reportResult").innerHTML=errorView(e.message);}}
function settingsView(){return `<section class="panel"><div class="panel-head"><h4>إعدادات النظام</h4></div><div class="settings-grid">
 <div>قاعدة البيانات<strong>SQL Server + EF Core</strong></div><div>المصادقة<strong>جلسات مخزنة وقاعدة صلاحيات</strong></div><div>المحاسبة<strong>قيد مزدوج وترحيل مستندات</strong></div><div>التدقيق<strong>Audit Log لكل عملية</strong></div></div></section>`;}

(async function bootstrap(){
 if(!state.token){renderLogin();return;}
 try{const me=await api("/api/auth/me");state.user=me.user;state.permissions=me.permissions||[];state.navigation=me.navigation||[];state.company=me.company;state.branches=me.branches||[];state.module="dashboard";renderShell();await loadModule("dashboard");}
 catch{state.token=null;localStorage.removeItem("saqer_token");renderLogin();}
})();
