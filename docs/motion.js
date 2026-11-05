(function () {
  var root = document.documentElement;
  var reduceQuery = window.matchMedia("(prefers-reduced-motion: reduce)");

  function reduced() {
    return reduceQuery.matches;
  }

  function applyLang(lang, persist) {
    var en = lang === "en";
    root.classList.toggle("lang-en", en);
    root.lang = en ? "en" : "ru";
    document.querySelectorAll("[data-set-lang]").forEach(function (btn) {
      var on = btn.getAttribute("data-set-lang") === (en ? "en" : "ru");
      btn.setAttribute("aria-pressed", on ? "true" : "false");
    });
    document.querySelectorAll("[data-label-ru]").forEach(function (el) {
      var label = en ? el.getAttribute("data-label-en") : el.getAttribute("data-label-ru");
      if (label) el.setAttribute("aria-label", label);
    });
    var title = document.querySelector("title");
    if (title && title.dataset.ru && title.dataset.en) {
      title.textContent = en ? title.dataset.en : title.dataset.ru;
    }
    if (persist) {
      try { localStorage.setItem("aeroway-docs-lang", en ? "en" : "ru"); } catch (err) { /* private mode */ }
    }
  }

  if (document.querySelector("[data-set-lang]")) {
    applyLang(root.classList.contains("lang-en") ? "en" : "ru", false);
    document.addEventListener("click", function (event) {
      var target = event.target;
      var btn = target && target.closest ? target.closest("[data-set-lang]") : null;
      if (!btn) return;
      var anchor = typeof activeId === "string" && activeId ? document.getElementById(activeId) : null;
      var before = anchor ? anchor.getBoundingClientRect().top : 0;
      applyLang(btn.getAttribute("data-set-lang"), true);
      if (!anchor) return;
      var delta = anchor.getBoundingClientRect().top - before;
      if (Math.abs(delta) < 1) return;
      var prev = root.style.scrollBehavior;
      root.style.scrollBehavior = "auto";
      window.scrollBy(0, delta);
      root.style.scrollBehavior = prev;
    });
  }

  function staggerDelay(el) {
    var parent = el.parentElement;
    if (!parent || !parent.hasAttribute("data-stagger")) return 0;
    var index = 0;
    for (var i = 0; i < parent.children.length; i++) {
      var child = parent.children[i];
      if (!child.hasAttribute("data-reveal")) continue;
      if (child === el) return Math.min(index, 8) * 110;
      index += 1;
    }
    return 0;
  }

  function easeOutQuart(t) {
    return 1 - Math.pow(1 - t, 4);
  }

  function snapCount(el) {
    el.setAttribute("data-counted", "1");
    el.textContent = el.getAttribute("data-count") || el.textContent;
  }

  function countUp(el) {
    if (el.getAttribute("data-counted") === "1") return;
    var target = parseInt(el.getAttribute("data-count"), 10);
    if (!isFinite(target)) return;
    el.setAttribute("data-counted", "1");
    if (reduced()) {
      el.textContent = String(target);
      return;
    }
    var start = performance.now();
    var duration = 1500;
    el.textContent = "0";
    function frame(now) {
      if (reduced()) {
        el.textContent = String(target);
        return;
      }
      var t = Math.min(1, (now - start) / duration);
      el.textContent = String(Math.round(target * easeOutQuart(t)));
      if (t < 1) requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
  }

  function startCounters(scope) {
    if (scope.hasAttribute("data-count")) countUp(scope);
    scope.querySelectorAll("[data-count]").forEach(countUp);
  }

  var reveals = Array.prototype.slice.call(document.querySelectorAll("[data-reveal]"));

  function show(el) {
    if (el.getAttribute("data-shown") === "1") return;
    el.setAttribute("data-shown", "1");
    if (!reduced()) el.style.animationDelay = staggerDelay(el) + "ms";
    el.classList.add("is-in");
    startCounters(el);
  }

  if (reduced() || !("IntersectionObserver" in window)) {
    reveals.forEach(show);
  } else if (reveals.length) {
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        show(entry.target);
        observer.unobserve(entry.target);
      });
    }, { rootMargin: "0px 0px -12% 0px", threshold: 0 });
    reveals.forEach(function (el) { observer.observe(el); });
  }

  reduceQuery.addEventListener("change", function () {
    if (!reduced()) return;
    reveals.forEach(function (el) {
      el.style.animationDelay = "0ms";
      el.classList.add("is-in");
    });
    document.querySelectorAll("[data-count]").forEach(snapCount);
  });

  var links = Array.prototype.slice.call(document.querySelectorAll("[data-nav]"));
  var seen = {};
  var sections = [];
  links.forEach(function (link) {
    var id = (link.getAttribute("href") || "").replace(/^#/, "");
    if (!id || seen[id]) return;
    var section = document.getElementById(id);
    if (!section) return;
    seen[id] = true;
    sections.push(section);
  });

  var activeId = "";

  function updateSpy() {
    if (!sections.length) return;
    var bar = document.querySelector(".topbar");
    var line = (bar ? bar.getBoundingClientRect().bottom : 120) + 72;
    var current = "";
    for (var i = 0; i < sections.length; i++) {
      if (sections[i].getBoundingClientRect().top <= line) current = sections[i].id;
    }
    if (current === activeId) return;
    activeId = current;
    links.forEach(function (link) {
      var on = !!current && (link.getAttribute("href") || "") === "#" + current;
      link.classList.toggle("is-active", on);
      if (on) link.setAttribute("aria-current", "location");
      else link.removeAttribute("aria-current");
    });
    if (!current) return;
    var scroller = document.querySelector(".nav-scroll");
    var chip = scroller ? scroller.querySelector("a.is-active") : null;
    if (!chip || !scroller) return;
    var left = chip.offsetLeft - (scroller.clientWidth - chip.offsetWidth) / 2;
    scroller.scrollTo({
      left: Math.max(0, left),
      behavior: reduced() ? "auto" : "smooth"
    });
  }

  if (sections.length) {
    var ticking = false;
    window.addEventListener("scroll", function () {
      if (ticking) return;
      ticking = true;
      requestAnimationFrame(function () {
        ticking = false;
        updateSpy();
      });
    }, { passive: true });
    window.addEventListener("hashchange", updateSpy);
    window.addEventListener("load", updateSpy);
    updateSpy();
  }

  root.classList.add("motion-ready");
})();
