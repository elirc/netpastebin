import React from "react";
import { createRoot } from "react-dom/client";
import PasteWorkspace from "./PasteWorkspace.jsx";
import "./styles.css";

createRoot(document.getElementById("root")).render(
  <React.StrictMode>
    <PasteWorkspace />
  </React.StrictMode>,
);
