const state = {
  token: localStorage.getItem("saqer_token"),
  user: null,
  navigation: [],
  module: "dashboard"
};

const icons = {
  dashboard: "▦",
  companies: "⌂",
  customers: "◉",
  suppliers: "◎",
  items: "▤",
  sales: "▣",
  purchases: "▥",
  accounts: "◫",
  inventory: "◈",
  reports: "◰",
  settings: "⚙"
};

const labels = {
  dashboard: "لوحة التحكم",
  companies: "الشركات والفروع",
  customers: "العملاء",
  suppliers: "الموردون",
  items: "الأصناف والمخزون",
  sales: "المبيعات",
  purchases: "المشتريات",
  accounts: "دليل الحسابات",
  inventory: "حركات المخزون",
  reports: "التقارير المالية",
  settings: "الإعدادات"
};

function esc(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

async function api(url, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (state.token) headers.Authorization = `Bearer ${state.token}`;
  if (options.body) headers["Content-Type"] = "application/json";

  const response = await fetch(url, { ...options, headers });
  if (response.status === 401) {
    state.token = null;
    localStorage.removeItem("saqer_token");
    renderLogin();
    throw new Error("انتهت الجلسة");
  }
  const data = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(data.message || "تعذر تنفيذ الطلب");
  return data;
}

function renderLogin(error = "") {
  document.getElementById("app").innerHTML = `
    <main class="auth-page">
      <section class="auth-panel">
        <div class="logo-mark">ص</div>
        <div>
          <div class="eyebrow">SAQER ERP</div>
          <h1>نظام صقر للمحاسبة</h1>
          <p>منصة موحدة للمبيعات والمشتريات والحسابات والمخزون والتقارير.</p>
        </div>
        <div class="feature-row">
          <span>المحاسبة</span><span>الفواتير</span><span>المخزون</span><span>التقارير</span>
        </div>
      </section>
      <section class="auth-card">
        <div class="card-title">تسجيل الدخول</div>
        <div class="muted">استخدم أحد الحسابين التجريبيين</div>
        <form id="loginForm" class="form-grid">
          <label>اسم المستخدم<input id="username" value="admin" autocomplete="username"></label>
          <label>كلمة المرور<input id="password" type="password" value="admin123" autocomplete="current-password"></label>
          <button class="primary" type="submit">دخول النظام</button>
        </form>
        <div class="demo-box">
          <div><strong>مدير النظام:</strong> admin / admin123</div>
          <div><strong>المحاسب:</strong> accountant / 123456</div>
        </div>
        ${error ? `<div class="error">${esc(error)}</div>` : ""}
      </section>
    </main>`;

  document.getElementById("loginForm").addEventListener("submit", async (event) => {
    event.preventDefault();
    try {
      const data = await api("/api/auth/login", {
        method: "POST",
        body: JSON.stringify({
          username: document.getElementById("username").value,
          password: document.getElementById("password").value
        })
      });
      state.token = data.token;
      state.user = data.user;
      state.navigation = data.navigation;
      localStorage.setItem("saqer_token", state.token);
      renderShell();
      await loadModule("dashboard");
    } catch {
      renderLogin("بيانات الدخول غير صحيحة.");
    }
  });
}

function renderShell() {
  document.getElementById("app").innerHTML = `
    <div class="shell">
      <aside class="sidebar">
        <div class="brand">
          <div class="logo-mark small">ص</div>
          <div><strong>صقر</strong><span>ERP</span></div>
        </div>
        <div class="sidebar-user">
          <div class="avatar">${esc((state.user?.FullName || state.user?.fullName || "م").slice(0,1))}</div>
          <div><strong>${esc(state.user?.FullName || state.user?.fullName)}</strong><small>${esc(state.user?.Group || state.user?.group)}</small></div>
        </div>
        <nav id="nav"></nav>
        <button class="logout" id="logoutBtn">تسجيل الخروج</button>
      </aside>
      <main class="main">
        <header class="topbar">
          <div>
            <div class="eyebrow">نظام صقر للمحاسبة</div>
            <h2 id="pageTitle">لوحة التحكم</h2>
          </div>
          <div class="top-actions">
            <span class="status-dot">متصل</span>
            <span class="date-chip">05 أكتوبر 2026</span>
          </div>
        </header>
        <section id="content" class="content"></section>
      </main>
    </div>`;

  document.getElementById("logoutBtn").onclick = async () => {
    try { await api("/api/auth/logout", { method: "POST" }); } catch {}
    state.token = null;
    localStorage.removeItem("saqer_token");
    renderLogin();
  };

  buildNav();
}

function buildNav() {
  const nav = document.getElementById("nav");
  nav.innerHTML = state.navigation.map(item => `
    <button class="nav-item ${item.key === state.module ? "active" : ""}" data-key="${esc(item.key)}">
      <span class="nav-icon">${icons[item.key] || "•"}</span>
      <span>${esc(item.title || item.Title)}</span>
    </button>`).join("");

  nav.querySelectorAll(".nav-item").forEach(btn => {
    btn.onclick = async () => {
      state.module = btn.dataset.key;
      buildNav();
      await loadModule(state.module);
    };
  });
}

async function loadModule(module) {
  document.getElementById("pageTitle").textContent = labels[module] || "النظام";
  const content = document.getElementById("content");

  if (module === "dashboard") {
    const data = await api("/api/dashboard");
    content.innerHTML = dashboardView(data);
    return;
  }

  content.innerHTML = '<div class="loading">جارٍ تحميل البيانات...</div>';
  const data = await api(`/api/modules/${module}`);
  content.innerHTML = moduleView(module, data);
}

function dashboardView(data) {
  return `
    <div class="hero">
      <div>
        <div class="eyebrow">نظرة عامة</div>
        <h3>مرحبًا ${esc(state.user?.FullName || state.user?.fullName)} 👋</h3>
        <p>كل ما تحتاجه لمتابعة النشاط المالي والتشغيلي في شاشة واحدة.</p>
      </div>
      <div class="hero-badge">Login → Group → Permission → Screen → Operation</div>
    </div>
    <div class="cards">
      ${data.cards.map(c => `
        <article class="metric">
          <div class="metric-top"><span>${esc(c.title)}</span><span class="metric-icon">${icons[c.key] || "◌"}</span></div>
          <strong>${Number(c.value).toLocaleString("ar-SA")} ر.س</strong>
          <small class="${c.trend >= 0 ? "up" : "down"}">${c.trend >= 0 ? "▲" : "▼"} ${Math.abs(c.trend)}%</small>
        </article>`).join("")}
    </div>
    <div class="grid-two">
      <section class="panel">
        <div class="panel-head"><h4>آخر الفواتير</h4><span>محدث الآن</span></div>
        <div class="table-wrap"><table><thead><tr><th>رقم الفاتورة</th><th>الطرف</th><th>التاريخ</th><th>المبلغ</th><th>الحالة</th></tr></thead>
        <tbody>${data.recentInvoices.map(x => `<tr><td>${esc(x.number)}</td><td>${esc(x.party)}</td><td>${esc(x.date)}</td><td>${Number(x.amount).toLocaleString("ar-SA")} ر.س</td><td><span class="pill">${esc(x.status)}</span></td></tr>`).join("")}</tbody></table></div>
      </section>
      <section class="panel">
        <div class="panel-head"><h4>النشاط الأخير</h4><span>اليوم</span></div>
        <div class="activity">${data.activity.map(x => `<div class="activity-row"><span class="activity-time">${esc(x.time)}</span><span class="activity-dot"></span><div>${esc(x.text)}<small>تمت العملية بنجاح</small></div></div>`).join("")}</div>
      </section>
    </div>`;
}

function moduleView(module, data) {
  if (!Array.isArray(data)) {
    return `<section class="panel"><div class="panel-head"><h4>إعدادات النظام</h4></div><div class="empty">النسخة الأساسية جاهزة للتوسعة وربط قاعدة البيانات.</div></section>`;
  }

  if (!data.length) {
    return `<section class="panel"><div class="empty">لا توجد بيانات حالياً.</div></section>`;
  }

  const columns = Object.keys(data[0]);
  return `
    <section class="panel">
      <div class="panel-head">
        <div><h4>${labels[module]}</h4><span>${data.length} سجل</span></div>
        <button class="secondary" onclick="alert('سيتم ربط عملية الإضافة مع قاعدة البيانات في المرحلة التالية.')">إضافة جديد</button>
      </div>
      <div class="table-wrap">
        <table><thead><tr>${columns.map(c => `<th>${esc(c)}</th>`).join("")}</tr></thead>
        <tbody>${data.map(row => `<tr>${columns.map(c => `<td>${esc(row[c])}</td>`).join("")}</tr>`).join("")}</tbody>
        </table>
      </div>
    </section>`;
}

(async function bootstrap() {
  if (!state.token) {
    renderLogin();
    return;
  }

  try {
    const me = await api("/api/auth/me");
    state.user = me.user;
    state.navigation = me.navigation;
    state.module = "dashboard";
    renderShell();
    await loadModule("dashboard");
  } catch {
    renderLogin();
  }
})();
