import * as vscode from "vscode";
import * as fs from "fs";
import * as crypto from "node:crypto";

function getNonce(): string {
  return crypto.randomBytes(16).toString("base64");
}

export function getWebviewHTML(webview: vscode.Webview, extensionURI: vscode.Uri): string {
  const nonce = getNonce();

  const mermaidLibUriStr = "https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.esm.min.js";

  const scriptPathOnDisk = vscode.Uri.joinPath(extensionURI, "out", "assets", "webview.js");
  const webviewScriptUri = webview.asWebviewUri(scriptPathOnDisk);

  const cspSource = webview.cspSource;
  const contentSecurityPolicy = `
    default-src 'none';
    style-src ${cspSource} 'unsafe-inline';
    img-src ${cspSource} data;
    script-src  'nonce-${nonce}'
    ${webview.cspSource}
    ${mermaidLibUriStr.startsWith("https://cdn.jsdelivr.net") ? "https://cdn.jsdelivr.net" : ""};
    connect-src 'none';
  `;

  const htmlFilePath = vscode.Uri.joinPath(extensionURI, "out", "assets", "webview.html");
  let htmlContent = "";
  try {
    htmlContent = fs.readFileSync(htmlFilePath.fsPath, "utf8");
  } catch (err) {
    console.log(err);
  }

  htmlContent = htmlContent.replace(/${csp}/g, contentSecurityPolicy);
  htmlContent = htmlContent.replace(/${nonce}/g, nonce);
  htmlContent = htmlContent.replace(/${mermaidUri}/g, mermaidLibUriStr);
  htmlContent = htmlContent.replace(/${webviewScriptUri}/g, webviewScriptUri.toString());

  return htmlContent;
}
