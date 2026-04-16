// Ambient declarations for the Metronic 9 starterKit components we
// forked into `components/ui/*.jsx` (Pitfall 17 — ship JS untouched).
//
// `allowJs: true` + `checkJs: false` tells `tsc` to read the `.jsx`
// files for shape inference but not to type-check them. Because the
// starterKit components use defaulted destructured props
// (`function Button({ variant = 'primary', ... }) {}`) TypeScript infers
// each destructured identifier as "required but defaulted" — so every
// call site would have to pass `variant`, `shape`, `size`, etc.
//
// The shims below restore the intended shape: each component takes any
// HTMLAttributes-style prop bag and renders a React element. This is
// the same contract Metronic documents for these components.

import type { ComponentType, ReactNode } from 'react';

type AnyProps = {
  children?: ReactNode;
  className?: string;
  [key: string]: unknown;
};

declare module '@/components/ui/button' {
  export const Button: ComponentType<AnyProps>;
  export const buttonVariants: (...args: unknown[]) => string;
}

declare module '@/components/ui/tabs' {
  export const Tabs: ComponentType<AnyProps>;
  export const TabsList: ComponentType<AnyProps>;
  export const TabsTrigger: ComponentType<AnyProps>;
  export const TabsContent: ComponentType<AnyProps>;
}

declare module '@/components/ui/sonner' {
  export const Toaster: ComponentType<AnyProps>;
}

declare module '@/components/ui/tooltip' {
  export const TooltipProvider: ComponentType<AnyProps>;
  export const Tooltip: ComponentType<AnyProps>;
  export const TooltipTrigger: ComponentType<AnyProps>;
  export const TooltipContent: ComponentType<AnyProps>;
}
