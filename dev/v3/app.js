/* PocketMic LAN v3 — progressive enhancement, zero dependencies */
(function () {
  "use strict";

  function ready(fn) {
    if (document.readyState !== "loading") fn();
    else document.addEventListener("DOMContentLoaded", fn, { once: true });
  }

  ready(function () {
    safeSetYear();
    safeMobileNav();
    safeScrollSpy();
    setupChecklist();
    setupScreenshotModal();
    setupMobileCta();
  });

  // --- Year ---
  function safeSetYear() {
    var el = document.getElementById("year");
    if (!el || typeof el.textContent !== "string") return;
    var y = new Date().getFullYear();
    if (!Number.isFinite(y)) return;
    el.textContent = String(y);
  }

  // --- Mobile nav ---
  function safeMobileNav() {
    var toggle = document.getElementById("nav-toggle");
    var nav = document.getElementById("nav-list");
    if (!toggle || !nav || typeof toggle.addEventListener !== "function") return;

    function closeNav(restoreFocus) {
      if (!toggle.checked) return;
      toggle.checked = false;
      if (restoreFocus && typeof toggle.focus === "function") toggle.focus();
    }

    toggle.setAttribute("aria-controls", "nav-list");

    // Sync aria-expanded for screen readers.
    var navWrap = toggle.closest(".primary-nav");
    function syncExpanded() {
      if (navWrap) navWrap.setAttribute("aria-expanded", String(toggle.checked));
    }
    toggle.addEventListener("change", syncExpanded);
    syncExpanded();

    Array.prototype.slice.call(nav.querySelectorAll("a[href^='#']"))
      .forEach(function (link) {
        link.addEventListener("click", function () { closeNav(false); });
      });

    document.addEventListener("keydown", function (event) {
      if (event.key !== "Escape") return;
      if (!toggle.checked) return;
      event.preventDefault();
      closeNav(true);
    });
  }

  // --- Scroll spy ---
  function safeScrollSpy() {
    var links = Array.prototype.slice.call(
      document.querySelectorAll(".nav-list a[href^='#']")
    );
    if (!links.length || !("IntersectionObserver" in window)) return;
    var sections = [];
    links.forEach(function (a) {
      var id = a.getAttribute("href").slice(1);
      var sec = document.getElementById(id);
      if (sec) sections.push({ link: a, section: sec });
    });
    if (!sections.length) return;

    var visible = [];
    var activeSection = null;

    function setActive(section) {
      if (!section || activeSection === section) return;
      activeSection = section;
      sections.forEach(function (s) {
        var active = s === section;
        if (s.link.classList) s.link.classList.toggle("is-active", active);
        if (active) s.link.setAttribute("aria-current", "location");
        else s.link.removeAttribute("aria-current");
      });
    }

    function updateActive() {
      if (!visible.length) return;
      var viewportMidpoint = window.innerHeight / 2;
      visible.sort(function (a, b) {
        return Math.abs(a.section.getBoundingClientRect().top - viewportMidpoint) -
          Math.abs(b.section.getBoundingClientRect().top - viewportMidpoint);
      });
      setActive(visible[0]);
    }

    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        var match = sections.find(function (s) { return s.section === e.target; });
        if (!match) return;
        visible = visible.filter(function (s) { return s !== match; });
        if (e.isIntersecting) visible.push(match);
      });
      updateActive();
    }, { rootMargin: "-40% 0px -55% 0px", threshold: 0 });
    sections.forEach(function (s) { io.observe(s.section); });
  }

  // --- Setup checklist ---
  function setupChecklist() {
    var items = document.querySelectorAll(".check-box");
    if (!items.length) return;
    items.forEach(function (box) {
      box.addEventListener("click", function () {
        box.classList.toggle("is-done");
        updateChecklistProgress();
      });
      box.addEventListener("keydown", function (e) {
        if (e.key === "Enter" || e.key === " ") {
          e.preventDefault();
          box.click();
        }
      });
    });
  }

  function updateChecklistProgress() {
    var items = document.querySelectorAll(".check-box");
    var done = document.querySelectorAll(".check-box.is-done");
    var label = document.getElementById("checklist-progress");
    if (label) {
      label.textContent = done.length + " of " + items.length + " checked";
    }
    var resetBtn = document.getElementById("checklist-reset");
    if (resetBtn) {
      resetBtn.style.display = done.length > 0 ? "" : "none";
    }
  }

  // --- Screenshot modal ---
  function setupScreenshotModal() {
    var links = document.querySelectorAll("[data-image-preview]");
    if (!links.length) return;
    var dialog = document.getElementById("screenshot-dialog");
    if (!dialog) return;
    var img = dialog.querySelector("img");
    var closeBtn = dialog.querySelector("[data-close-dialog]");

    links.forEach(function (link) {
      link.addEventListener("click", function (e) {
        e.preventDefault();
        img.src = link.href;
        img.alt = link.getAttribute("aria-label") || "";
        dialog.showModal();
      });
    });

    if (closeBtn) closeBtn.addEventListener("click", function () { dialog.close(); });
    dialog.addEventListener("click", function (e) {
      if (e.target === dialog) dialog.close();
    });
  }

  // --- Mobile CTA visibility ---
  function setupMobileCta() {
    var cta = document.querySelector(".mobile-cta");
    if (!cta) return;
    var hero = document.getElementById("hero");
    if (!hero || !("IntersectionObserver" in window)) return;
    var io = new IntersectionObserver(function (entries) {
      cta.style.transform = entries[0].isIntersecting ? "translateY(100%)" : "translateY(0)";
    }, { threshold: 0 });
    io.observe(hero);
  }

  // --- Toast ---
  window.pocketmicToast = function (message) {
    var el = document.getElementById("status-message");
    if (!el) return;
    el.textContent = message;
    clearTimeout(el._timer);
    el._timer = setTimeout(function () { el.textContent = ""; }, 4000);
  };
})();
