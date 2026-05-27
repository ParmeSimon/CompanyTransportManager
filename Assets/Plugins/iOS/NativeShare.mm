#import <UIKit/UIKit.h>

extern "C" {
    void _ShareText(const char* text) {
        NSString* shareText = [NSString stringWithUTF8String:text];
        UIActivityViewController* vc =
            [[UIActivityViewController alloc] initWithActivityItems:@[shareText]
                                              applicationActivities:nil];

        UIViewController* root = [UIApplication sharedApplication]
                                     .keyWindow.rootViewController;
        // Walk presented chain to reach the topmost controller
        while (root.presentedViewController)
            root = root.presentedViewController;

        // iPad requires a source rect for the popover
        if ([UIDevice currentDevice].userInterfaceIdiom == UIUserInterfaceIdiomPad) {
            vc.popoverPresentationController.sourceView = root.view;
            vc.popoverPresentationController.sourceRect =
                CGRectMake(CGRectGetMidX(root.view.bounds),
                           CGRectGetMidY(root.view.bounds), 0, 0);
            vc.popoverPresentationController.permittedArrowDirections = 0;
        }

        [root presentViewController:vc animated:YES completion:nil];
    }
}
