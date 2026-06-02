#import <UIKit/UIKit.h>

// Fournie par le runtime Unity sur iOS.
extern "C" void UnitySendMessage(const char* obj, const char* method, const char* msg);

@interface ImagePickerDelegate : NSObject <UIImagePickerControllerDelegate, UINavigationControllerDelegate>
@property (nonatomic, strong) NSString* callbackObject;
@end

static ImagePickerDelegate* _pickerDelegate = nil;

@implementation ImagePickerDelegate

- (UIViewController*)topController {
    UIViewController* root = [UIApplication sharedApplication].keyWindow.rootViewController;
    while (root.presentedViewController) root = root.presentedViewController;
    return root;
}

- (void)notify:(NSString*)path {
    UnitySendMessage([self.callbackObject UTF8String], "OnNativeImagePicked",
                     [(path ?: @"") UTF8String]);
}

- (void)imagePickerController:(UIImagePickerController *)picker
        didFinishPickingMediaWithInfo:(NSDictionary<UIImagePickerControllerInfoKey,id> *)info {
    UIImage* image = info[UIImagePickerControllerOriginalImage];
    __block NSString* savedPath = @"";

    if (image) {
        NSData* data = UIImagePNGRepresentation(image);
        if (data) {
            NSString* tmp = [NSTemporaryDirectory() stringByAppendingPathComponent:@"picked_logo.png"];
            if ([data writeToFile:tmp atomically:YES]) savedPath = tmp;
        }
    }

    __weak ImagePickerDelegate* weakSelf = self;
    [picker dismissViewControllerAnimated:YES completion:^{
        [weakSelf notify:savedPath];
        _pickerDelegate = nil;
    }];
}

- (void)imagePickerControllerDidCancel:(UIImagePickerController *)picker {
    __weak ImagePickerDelegate* weakSelf = self;
    [picker dismissViewControllerAnimated:YES completion:^{
        [weakSelf notify:@""];
        _pickerDelegate = nil;
    }];
}

@end

extern "C" {
    void _PickImage(const char* gameObjectName) {
        _pickerDelegate = [[ImagePickerDelegate alloc] init];
        _pickerDelegate.callbackObject = [NSString stringWithUTF8String:gameObjectName];

        UIImagePickerController* pc = [[UIImagePickerController alloc] init];
        pc.sourceType = UIImagePickerControllerSourceTypePhotoLibrary;
        pc.delegate = _pickerDelegate;
        pc.modalPresentationStyle = UIModalPresentationFullScreen;

        [[_pickerDelegate topController] presentViewController:pc animated:YES completion:nil];
    }
}
