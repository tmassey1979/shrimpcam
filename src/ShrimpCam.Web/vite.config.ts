import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { VitePWA } from "vite-plugin-pwa";

export default defineConfig({
  server: {
    proxy: {
      "/audit": "http://localhost:5098",
      "/auth": "http://localhost:5098",
      "/backups": "http://localhost:5098",
      "/captures": "http://localhost:5098",
      "/diagnostics": "http://localhost:5098",
      "/health": "http://localhost:5098",
      "/settings": "http://localhost:5098",
      "/stream": "http://localhost:5098"
    }
  },
  plugins: [
    react(),
    VitePWA({
      registerType: "autoUpdate",
      includeAssets: ["icons/icon.svg"],
      manifest: {
        name: "Shrimp Cam",
        short_name: "Shrimp Cam",
        description: "Mobile-first control surface for monitoring Shrimp Cam.",
        theme_color: "#0f5f73",
        background_color: "#081a20",
        display: "standalone",
        start_url: "/",
        scope: "/",
        icons: [
          {
            src: "/icons/icon.svg",
            sizes: "any",
            type: "image/svg+xml",
            purpose: "any"
          }
        ]
      },
      workbox: {
        globPatterns: ["**/*.{js,css,html,svg,png}"]
      }
    })
  ]
});
