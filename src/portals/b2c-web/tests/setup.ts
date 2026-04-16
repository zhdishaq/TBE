// Vitest setup — registers jest-dom matchers (toBeInTheDocument etc.).
// No global fetch mocks; MSW handlers are loaded per-suite where needed.

import '@testing-library/jest-dom';
