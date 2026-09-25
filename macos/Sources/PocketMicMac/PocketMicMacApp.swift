import SwiftUI

@main
struct PocketMicMacApp: App {
    var body: some Scene {
        WindowGroup {
            ReceiverView()
                .frame(minWidth: 460, minHeight: 460)
        }
    }
}
