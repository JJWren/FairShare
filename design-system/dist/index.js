// src/components/WarmProvider.tsx
import { jsx } from "react/jsx-runtime";
function WarmProvider({ theme = "light", children }) {
  return /* @__PURE__ */ jsx("div", { className: "fs-root", "data-fs-theme": theme === "dark" ? "dark" : void 0, style: { minHeight: "100%", padding: 1 }, children });
}

// src/components/Button.tsx
import { jsx as jsx2 } from "react/jsx-runtime";
function Button({ variant = "primary", className, ...rest }) {
  const variantClass = variant === "outline-danger" ? " fs-btn--outline-danger" : variant === "outline-accent" ? " fs-btn--outline-accent" : "";
  return /* @__PURE__ */ jsx2("button", { type: "button", className: `fs-btn${variantClass}${className ? ` ${className}` : ""}`, ...rest });
}

// src/components/MoneyInput.tsx
import { useId } from "react";
import { jsx as jsx3, jsxs } from "react/jsx-runtime";
function MoneyInput({ label, hint, id, ...rest }) {
  const autoId = useId();
  const inputId = id ?? autoId;
  return /* @__PURE__ */ jsxs("div", { children: [
    /* @__PURE__ */ jsx3("label", { className: "fs-field-label", htmlFor: inputId, children: label }),
    /* @__PURE__ */ jsxs("div", { className: "fs-money", children: [
      /* @__PURE__ */ jsx3("span", { className: "fs-money__prefix", children: "$" }),
      /* @__PURE__ */ jsx3("input", { id: inputId, className: "fs-money__input", inputMode: "decimal", min: 0, type: "number", ...rest })
    ] }),
    hint ? /* @__PURE__ */ jsx3("div", { className: "fs-field-hint", children: hint }) : null
  ] });
}

// src/components/PartyBadge.tsx
import { jsx as jsx4 } from "react/jsx-runtime";
function PartyBadge({ party, children }) {
  return /* @__PURE__ */ jsx4("span", { className: `fs-party-badge fs-party-badge--${party}`, children: children ?? (party === "plaintiff" ? "Plaintiff" : "Defendant") });
}

// src/components/PartyCard.tsx
import { jsx as jsx5, jsxs as jsxs2 } from "react/jsx-runtime";
function PartyCard({ party, headerRight, children }) {
  return /* @__PURE__ */ jsxs2("section", { className: `fs-party-card fs-party-card--${party}`, children: [
    /* @__PURE__ */ jsxs2("header", { className: "fs-party-card__header", children: [
      /* @__PURE__ */ jsx5(PartyBadge, { party }),
      headerRight
    ] }),
    /* @__PURE__ */ jsx5("div", { className: "fs-party-card__body", children })
  ] });
}

// src/components/Toggle.tsx
import { jsx as jsx6, jsxs as jsxs3 } from "react/jsx-runtime";
function Toggle({ checked, label, onChange }) {
  return /* @__PURE__ */ jsxs3(
    "span",
    {
      className: "fs-toggle",
      role: "switch",
      "aria-checked": checked,
      tabIndex: 0,
      onClick: () => onChange?.(!checked),
      children: [
        label,
        /* @__PURE__ */ jsx6("span", { className: `fs-toggle__track${checked ? " fs-toggle__track--on" : ""}`, children: /* @__PURE__ */ jsx6("span", { className: "fs-toggle__thumb" }) })
      ]
    }
  );
}

// src/components/WorksheetTable.tsx
import { jsx as jsx7, jsxs as jsxs4 } from "react/jsx-runtime";
function WorksheetTable({ rows }) {
  return /* @__PURE__ */ jsxs4("table", { className: "fs-worksheet", children: [
    /* @__PURE__ */ jsx7("thead", { children: /* @__PURE__ */ jsxs4("tr", { children: [
      /* @__PURE__ */ jsx7("th", { scope: "col", children: "#" }),
      /* @__PURE__ */ jsx7("th", { scope: "col", children: "Item" }),
      /* @__PURE__ */ jsx7("th", { scope: "col", className: "fs-worksheet--num", children: "Plaintiff" }),
      /* @__PURE__ */ jsx7("th", { scope: "col", className: "fs-worksheet--num", children: "Defendant" }),
      /* @__PURE__ */ jsx7("th", { scope: "col", className: "fs-worksheet--num", children: "Combined" })
    ] }) }),
    /* @__PURE__ */ jsx7("tbody", { children: rows.map((row) => /* @__PURE__ */ jsxs4("tr", { className: row.highlight ? "fs-worksheet__row--gold" : void 0, children: [
      /* @__PURE__ */ jsx7("td", { className: "fs-worksheet__line", children: row.line }),
      /* @__PURE__ */ jsx7("td", { children: row.label }),
      /* @__PURE__ */ jsx7("td", { className: "fs-worksheet--num", children: row.plaintiff ?? "\u2014" }),
      /* @__PURE__ */ jsx7("td", { className: "fs-worksheet--num", children: row.defendant ?? "\u2014" }),
      /* @__PURE__ */ jsx7("td", { className: "fs-worksheet--num", children: row.combined ?? "\u2014" })
    ] }, row.line)) })
  ] });
}

// src/components/NavBar.tsx
import { jsx as jsx8, jsxs as jsxs5 } from "react/jsx-runtime";
function NavBar({ brand = "FairShare", links = [], guest = false, signInLabel = "Sign in" }) {
  return /* @__PURE__ */ jsxs5("nav", { className: "fs-navbar", children: [
    /* @__PURE__ */ jsxs5("span", { style: { display: "flex", gap: 36, alignItems: "center" }, children: [
      /* @__PURE__ */ jsx8("a", { className: "fs-navbar__brand", href: "/", children: brand }),
      links.length > 0 ? /* @__PURE__ */ jsx8("span", { className: "fs-navbar__links", children: links.map((link) => /* @__PURE__ */ jsx8("a", { className: `fs-navbar__link${link.active ? " fs-navbar__link--active" : ""}`, href: link.href, children: link.label }, link.label)) }) : null
    ] }),
    /* @__PURE__ */ jsxs5("span", { style: { display: "flex", gap: 14, alignItems: "center" }, children: [
      guest ? /* @__PURE__ */ jsx8("span", { className: "fs-navbar__guest", children: "Guest" }) : null,
      /* @__PURE__ */ jsx8("a", { className: "fs-navbar__signin", href: "/login", children: signInLabel })
    ] })
  ] });
}
export {
  Button,
  MoneyInput,
  NavBar,
  PartyBadge,
  PartyCard,
  Toggle,
  WarmProvider,
  WorksheetTable
};
