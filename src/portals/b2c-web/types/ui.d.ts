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

// --- Wave 2 (Plan 04-02) search + checkout primitives --------------------

declare module '@/components/ui/popover' {
  export const Popover: ComponentType<AnyProps>;
  export const PopoverTrigger: ComponentType<AnyProps>;
  export const PopoverContent: ComponentType<AnyProps>;
  export const PopoverAnchor: ComponentType<AnyProps>;
}

declare module '@/components/ui/command' {
  export const Command: ComponentType<AnyProps>;
  export const CommandDialog: ComponentType<AnyProps>;
  export const CommandInput: ComponentType<AnyProps>;
  export const CommandList: ComponentType<AnyProps>;
  export const CommandEmpty: ComponentType<AnyProps>;
  export const CommandGroup: ComponentType<AnyProps>;
  export const CommandItem: ComponentType<AnyProps>;
  export const CommandSeparator: ComponentType<AnyProps>;
  export const CommandShortcut: ComponentType<AnyProps>;
}

declare module '@/components/ui/dialog' {
  export const Dialog: ComponentType<AnyProps>;
  export const DialogTrigger: ComponentType<AnyProps>;
  export const DialogContent: ComponentType<AnyProps>;
  export const DialogHeader: ComponentType<AnyProps>;
  export const DialogFooter: ComponentType<AnyProps>;
  export const DialogTitle: ComponentType<AnyProps>;
  export const DialogDescription: ComponentType<AnyProps>;
  export const DialogClose: ComponentType<AnyProps>;
  export const DialogOverlay: ComponentType<AnyProps>;
  export const DialogPortal: ComponentType<AnyProps>;
}

declare module '@/components/ui/select' {
  export const Select: ComponentType<AnyProps>;
  export const SelectGroup: ComponentType<AnyProps>;
  export const SelectValue: ComponentType<AnyProps>;
  export const SelectTrigger: ComponentType<AnyProps>;
  export const SelectContent: ComponentType<AnyProps>;
  export const SelectLabel: ComponentType<AnyProps>;
  export const SelectItem: ComponentType<AnyProps>;
  export const SelectSeparator: ComponentType<AnyProps>;
}

declare module '@/components/ui/calendar' {
  export const Calendar: ComponentType<AnyProps>;
}

declare module '@/components/ui/card' {
  export const Card: ComponentType<AnyProps>;
  export const CardHeader: ComponentType<AnyProps>;
  export const CardTitle: ComponentType<AnyProps>;
  export const CardDescription: ComponentType<AnyProps>;
  export const CardContent: ComponentType<AnyProps>;
  export const CardFooter: ComponentType<AnyProps>;
}

declare module '@/components/ui/input' {
  export const Input: ComponentType<AnyProps>;
}

declare module '@/components/ui/label' {
  export const Label: ComponentType<AnyProps>;
}

declare module '@/components/ui/checkbox' {
  export const Checkbox: ComponentType<AnyProps>;
}

declare module '@/components/ui/radio-group' {
  export const RadioGroup: ComponentType<AnyProps>;
  export const RadioGroupItem: ComponentType<AnyProps>;
}

declare module '@/components/ui/slider' {
  export const Slider: ComponentType<AnyProps>;
}

declare module '@/components/ui/separator' {
  export const Separator: ComponentType<AnyProps>;
}

declare module '@/components/ui/skeleton' {
  export const Skeleton: ComponentType<AnyProps>;
}

declare module '@/components/ui/badge' {
  export const Badge: ComponentType<AnyProps>;
  export const badgeVariants: (...args: unknown[]) => string;
}
