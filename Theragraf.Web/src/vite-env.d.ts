/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_AZURE_AD_TENANT_ID: string;
  readonly VITE_AZURE_AD_CLIENT_ID: string;
  readonly VITE_AZURE_AD_API_SCOPE: string;
  readonly VITE_SPEECH_SERVICE_REGION: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
