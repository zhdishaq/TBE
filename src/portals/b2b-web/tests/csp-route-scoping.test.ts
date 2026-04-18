// Plan 05-05 Task 1 — route-scoped CSP structural guard.
//
// Mitigation T-05-05-01 (Pitfall 5 / SAQ-A scope preservation):
//   - /admin/wallet/:path* matcher MUST allow https://js.stripe.com (script-src),
//     https://hooks.stripe.com (frame-src) and https://api.stripe.com (connect-src).
//   - Default matcher (/:path* or /((?!admin/wallet).*)) MUST omit all Stripe
//     origins so Stripe.js cannot load on /search, /checkout, /dashboard, etc.
//   - Both matchers MUST declare `default-src 'self'`; neither allows
//     `'unsafe-eval'` (belt-and-braces — legacy b2c-web previously included it).

import { describe, it, expect } from 'vitest';

interface HeaderEntry {
  key: string;
  value: string;
}
interface HeaderRule {
  source: string;
  headers: HeaderEntry[];
}

async function loadHeaders(): Promise<HeaderRule[]> {
  const mod = (await import('../next.config.mjs')) as {
    default: { headers?: () => Promise<HeaderRule[]> };
  };
  const rules = await mod.default.headers?.();
  return rules ?? [];
}

function findRule(rules: HeaderRule[], pattern: RegExp): HeaderRule {
  const rule = rules.find((r) => pattern.test(r.source));
  if (!rule) {
    throw new Error(
      `No rule matched ${pattern} — sources: ${rules.map((r) => r.source).join(', ')}`,
    );
  }
  return rule;
}

function cspValue(rule: HeaderRule): string {
  const entry = rule.headers.find((h) => h.key === 'Content-Security-Policy');
  if (!entry) throw new Error(`No CSP on rule ${rule.source}`);
  return entry.value;
}

describe('next.config.mjs route-scoped CSP', () => {
  it('permits Stripe origins on /admin/wallet/:path*', async () => {
    const rules = await loadHeaders();
    const wallet = findRule(rules, /admin\/wallet/);
    const csp = cspValue(wallet);
    expect(csp).toContain('https://js.stripe.com');
    expect(csp).toContain('https://hooks.stripe.com');
    expect(csp).toContain('https://api.stripe.com');
  });

  it('omits Stripe origins on the default matcher (Pitfall 5 / Pitfall 6 SAQ-A scope)', async () => {
    const rules = await loadHeaders();
    const defaultRule = rules.find(
      (r) => !/admin\/wallet/.test(r.source),
    );
    if (!defaultRule) throw new Error('No default matcher rule found');
    const csp = cspValue(defaultRule);
    expect(csp).not.toContain('js.stripe.com');
    expect(csp).not.toContain('hooks.stripe.com');
    expect(csp).not.toContain('api.stripe.com');
  });

  it('both matchers declare default-src \'self\' and do not allow unsafe-eval in script-src', async () => {
    const rules = await loadHeaders();
    expect(rules.length).toBeGreaterThanOrEqual(2);
    for (const rule of rules) {
      const csp = cspValue(rule);
      expect(csp).toContain("default-src 'self'");
      // Ensure no script-src enables 'unsafe-eval' anywhere (Pitfall 5 hardening).
      const scriptSrcLine = csp
        .split(';')
        .map((s) => s.trim())
        .find((s) => s.startsWith('script-src'));
      expect(scriptSrcLine, `script-src missing on ${rule.source}`).toBeDefined();
      expect(scriptSrcLine ?? '').not.toContain("'unsafe-eval'");
    }
  });
});
