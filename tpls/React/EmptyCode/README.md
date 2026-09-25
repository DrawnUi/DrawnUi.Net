# DrawnUI React app

A browser app with one DrawnUI canvas filling the window, built with React and Vite on the `drawnui-react` package. Your UI lives in `src/MainPage.tsx`.

## Run

```
npm install
npm run dev
```

Then open http://localhost:5173.

## Publish

```
npm run build
```

Upload the `dist` folder to any static host.

## Where things are

- `src/MainPage.tsx`: your UI.
- `src/main.tsx`: startup. Fonts are registered here, and the `<Canvas>` is created here.
- `public/fonts`: the fonts, loaded at startup. `OpenSans` is registered as `FontText` and `FontTextTitle`, the symbol and emoji subsets by `AddSymbols()` and `AddEmojis()`.
- `public`: images and other assets, addressed by file name (`Source="drawnui.svg"`).

## Learn more

- Demo app with a page per feature: https://helloreact.drawnui.net
- Docs: https://drawnui.net/articles/react/index.html
- Skill for AI agents: https://helloreact.drawnui.net/skills/drawnui-react/SKILL.md
