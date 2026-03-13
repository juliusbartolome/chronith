// @ts-check
import { defineConfig } from "astro/config";
import starlight from "@astrojs/starlight";

export default defineConfig({
  site: "https://docs.chronith.io",
  integrations: [
    starlight({
      title: "Chronith Docs",
      description: "Official documentation for the Chronith booking engine API.",
      social: [
        { icon: "github", label: "GitHub", href: "https://github.com/juliusbartolome/chronith" },
      ],
      sidebar: [
        { label: "Getting Started", autogenerate: { directory: "getting-started" } },
        { label: "Guides", autogenerate: { directory: "guides" } },
        { label: "API Reference", autogenerate: { directory: "api-reference" } },
        { label: "SDKs", autogenerate: { directory: "sdks" } },
        { label: "Architecture", autogenerate: { directory: "architecture" } },
        { label: "Changelog", link: "/changelog" },
      ],
      editLink: {
        baseUrl: "https://github.com/juliusbartolome/chronith/edit/main/docs/site/",
      },
    }),
  ],
});
