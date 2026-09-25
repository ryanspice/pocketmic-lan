/* PocketMic site locale preference: Canadian English fallback, US English spelling preview. */
(function () {
  "use strict";

  var DEFAULT_LOCALE = "en-CA";
  var US_LOCALE = "en-US";
  var STORAGE_KEY = "pocketmic-site-locale";
  var regionalWords = [
    [/\blicences?\b/gi, function (word) { return matchCase(word, word.replace(/licence/i, "license")); }],
    [/\bbehaviours?\b/gi, function (word) { return matchCase(word, word.replace(/behaviour/i, "behavior")); }]
  ];
  var originalText = new WeakMap();
  var originalAttributes = new WeakMap();
  var sourceTitle = document.title;
  var sourceMeta = new WeakMap();

  function matchCase(source, replacement) {
    if (source === source.toUpperCase()) return replacement.toUpperCase();
    if (source.charAt(0) === source.charAt(0).toUpperCase()) {
      return replacement.charAt(0).toUpperCase() + replacement.slice(1);
    }
    return replacement;
  }

  function toAmericanEnglish(value) {
    return regionalWords.reduce(function (text, entry) {
      return text.replace(entry[0], entry[1]);
    }, value);
  }

  function readPreference() {
    try {
      var stored = window.localStorage.getItem(STORAGE_KEY);
      if (stored === DEFAULT_LOCALE || stored === US_LOCALE) return stored;
    } catch (_) {
      // Storage can be unavailable in private or restricted browsing contexts.
    }
    return DEFAULT_LOCALE;
  }

  function applyLocale(locale) {
    document.documentElement.lang = locale;
    document.title = locale === US_LOCALE ? toAmericanEnglish(sourceTitle) : sourceTitle;

    var walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    var textNode;
    while ((textNode = walker.nextNode())) {
      var parent = textNode.parentElement;
      if (!parent || parent.closest("script,style,code,pre,kbd,samp,textarea,svg")) continue;
      if (!originalText.has(textNode)) originalText.set(textNode, textNode.nodeValue);
      var source = originalText.get(textNode);
      textNode.nodeValue = locale === US_LOCALE ? toAmericanEnglish(source) : source;
    }

    var attributeNames = ["alt", "aria-label", "title", "placeholder"];
    document.querySelectorAll("[alt],[aria-label],[title],[placeholder]").forEach(function (element) {
      var originals = originalAttributes.get(element) || {};
      attributeNames.forEach(function (name) {
        if (!element.hasAttribute(name)) return;
        if (!Object.prototype.hasOwnProperty.call(originals, name)) originals[name] = element.getAttribute(name);
        var source = originals[name];
        element.setAttribute(name, locale === US_LOCALE ? toAmericanEnglish(source) : source);
      });
      originalAttributes.set(element, originals);
    });

    document.querySelectorAll('meta[name="description"],meta[property="og:description"],meta[name="twitter:description"],meta[property="og:title"],meta[name="twitter:title"]').forEach(function (meta) {
      if (!sourceMeta.has(meta)) sourceMeta.set(meta, meta.getAttribute("content") || "");
      var source = sourceMeta.get(meta);
      meta.setAttribute("content", locale === US_LOCALE ? toAmericanEnglish(source) : source);
    });
  }

  function addLanguageControl(locale) {
    var header = document.querySelector(".site-header .header-inner, .site-header .header-row, .site-head .head-row");
    if (!header) return;

    var label = document.createElement("label");
    label.className = "site-locale";
    label.htmlFor = "site-locale-select";

    var caption = document.createElement("span");
    caption.className = "site-locale-caption";
    caption.textContent = "Website language";

    var select = document.createElement("select");
    select.id = "site-locale-select";
    select.setAttribute("aria-label", "Website language");
    select.title = "Canadian English is the fallback. US English is a spelling preview.";

    [
      { value: DEFAULT_LOCALE, text: "Canada · en-CA" },
      { value: US_LOCALE, text: "United States · en-US preview" }
    ].forEach(function (item) {
      var option = document.createElement("option");
      option.value = item.value;
      option.textContent = item.text;
      select.appendChild(option);
    });

    select.value = locale;
    select.addEventListener("change", function () {
      var next = select.value === US_LOCALE ? US_LOCALE : DEFAULT_LOCALE;
      try {
        window.localStorage.setItem(STORAGE_KEY, next);
      } catch (_) {
        // The selection still applies for this page when storage is unavailable.
      }
      applyLocale(next);
    });

    label.appendChild(caption);
    label.appendChild(select);
    var slot = header.querySelector("[data-site-locale-slot]");
    (slot || header).appendChild(label);
  }

  var locale = readPreference();
  applyLocale(locale);
  addLanguageControl(locale);
})();
