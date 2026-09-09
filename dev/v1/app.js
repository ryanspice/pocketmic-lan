/* PocketMic LAN landing page — progressive enhancement only.
 * Page must remain fully usable with JS disabled.
 */
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
  });

  function safeSetYear() {
    var el = document.getElementById("year");
    if (!el || typeof el.textContent !== "string") return;
    var y = new Date().getFullYear();
    if (!Number.isFinite(y)) return;
    el.textContent = String(y);
  }

  function safeMobileNav() {
    var toggle = document.getElementById("nav-toggle");
    var nav = document.getElementById("nav-list");
    if (!toggle || !nav || typeof toggle.addEventListener !== "function") return;

    function closeNav(restoreFocus) {
      if (!toggle.checked) return;
      toggle.checked = false;
      if (restoreFocus && typeof toggle.focus === "function") toggle.focus();
    }

    // The native checkbox state already conveys open/closed semantics. Keep
    // aria-controls for the relationship, but do not add aria-expanded: that
    // state is not supported on the checkbox role.
    toggle.setAttribute("aria-controls", "nav-list");

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

    // Sync aria-expanded on the nav wrapper for screen readers.
    var navWrap = toggle.closest(".primary-nav");
    function syncExpanded() {
      if (navWrap) navWrap.setAttribute("aria-expanded", String(toggle.checked));
    }
    toggle.addEventListener("change", syncExpanded);
    syncExpanded();
  }

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
})();
