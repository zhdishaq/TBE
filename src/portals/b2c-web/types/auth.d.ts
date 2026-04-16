// Module augmentation for Auth.js v5 Session and JWT.
//
// Adds the fields we persist during the jwt/session callbacks so
// downstream TypeScript consumers can read `session.access_token`,
// `session.email_verified`, etc. without casting to `any`.
//
// Source: Auth.js v5 docs "Module Augmentation" + 04-RESEARCH Pattern 1.

import 'next-auth';
import 'next-auth/jwt';

declare module 'next-auth' {
  interface Session {
    access_token?: string;
    email_verified: boolean;
    expires_at?: number;
    error?: 'RefreshAccessTokenError';
  }
}

declare module 'next-auth/jwt' {
  interface JWT {
    access_token?: string;
    refresh_token?: string;
    expires_at?: number;
    email_verified?: boolean;
    error?: 'RefreshAccessTokenError';
  }
}
