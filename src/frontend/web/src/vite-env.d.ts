/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_AAD_TENANT_ID?: string;
  readonly VITE_AAD_CLIENT_ID?: string;
  readonly VITE_AAD_AUTHORITY?: string;
  readonly VITE_AAD_REDIRECT_URI?: string;
  readonly VITE_API_SCOPE?: string;
  readonly VITE_API_BASE_URL?: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
