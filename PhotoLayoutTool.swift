import Cocoa
import WebKit

final class AppDelegate: NSObject, NSApplicationDelegate, WKNavigationDelegate, WKScriptMessageHandler {
    private var window: NSWindow!
    private var webView: WKWebView!

    func applicationDidFinishLaunching(_ notification: Notification) {
        let configuration = WKWebViewConfiguration()
        configuration.preferences.javaScriptCanOpenWindowsAutomatically = true
        configuration.userContentController.add(self, name: "saveImage")

        webView = WKWebView(frame: .zero, configuration: configuration)
        webView.navigationDelegate = self

        window = NSWindow(
            contentRect: NSRect(x: 0, y: 0, width: 1280, height: 860),
            styleMask: [.titled, .closable, .miniaturizable, .resizable],
            backing: .buffered,
            defer: false
        )
        window.center()
        window.title = "fiona索引制作工具"
        window.contentView = webView
        window.makeKeyAndOrderFront(nil)

        loadBundledPage()
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        true
    }

    private func loadBundledPage() {
        guard let htmlURL = Bundle.main.url(forResource: "index", withExtension: "html") else {
            showError("找不到 index.html")
            return
        }

        webView.loadFileURL(htmlURL, allowingReadAccessTo: htmlURL.deletingLastPathComponent())
    }

    private func showError(_ message: String) {
        let alert = NSAlert()
        alert.messageText = "启动失败"
        alert.informativeText = message
        alert.alertStyle = .critical
        alert.runModal()
    }

    func userContentController(_ userContentController: WKUserContentController, didReceive message: WKScriptMessage) {
        guard message.name == "saveImage" else { return }
        guard let body = message.body as? [String: Any],
              let dataURL = body["dataUrl"] as? String,
              let filename = body["filename"] as? String else {
            showSaveError("导出数据不完整")
            return
        }

        let parts = dataURL.split(separator: ",", maxSplits: 1, omittingEmptySubsequences: false)
        guard parts.count == 2, let data = Data(base64Encoded: String(parts[1])) else {
            showSaveError("无法解析导出的图片数据")
            return
        }

        let panel = NSSavePanel()
        panel.nameFieldStringValue = filename
        panel.canCreateDirectories = true
        panel.title = "保存排版图片"
        panel.beginSheetModal(for: window) { response in
            guard response == .OK, let url = panel.url else { return }
            do {
                try data.write(to: url, options: .atomic)
            } catch {
                self.showSaveError("保存失败：\(error.localizedDescription)")
            }
        }
    }

    private func showSaveError(_ message: String) {
        let alert = NSAlert()
        alert.messageText = "导出失败"
        alert.informativeText = message
        alert.alertStyle = .warning
        if let window = window {
            alert.beginSheetModal(for: window)
        } else {
            alert.runModal()
        }
    }
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.activate(ignoringOtherApps: true)
app.run()
