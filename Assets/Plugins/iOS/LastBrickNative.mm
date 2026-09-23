// Native iOS helpers called from C# via [DllImport("__Internal")].
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController();

extern "C" {

// Light haptic tap (Handheld.Vibrate is a long, strong buzz on iOS).
// style: 0 = light, 1 = medium, 2 = heavy
void _LB_ImpactHaptic(int style)
{
    UIImpactFeedbackStyle feedbackStyle = UIImpactFeedbackStyleLight;
    if (style == 1) feedbackStyle = UIImpactFeedbackStyleMedium;
    else if (style == 2) feedbackStyle = UIImpactFeedbackStyleHeavy;

    UIImpactFeedbackGenerator* generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:feedbackStyle];
    [generator prepare];
    [generator impactOccurred];
}

// Opens the native iOS share sheet with the given text.
void _LB_ShareText(const char* text, const char* subject)
{
    NSString* message = text ? [NSString stringWithUTF8String:text] : @"";
    NSString* subjectText = subject ? [NSString stringWithUTF8String:subject] : @"";

    dispatch_async(dispatch_get_main_queue(), ^{
        UIViewController* root = UnityGetGLViewController();
        UIActivityViewController* activity =
            [[UIActivityViewController alloc] initWithActivityItems:@[message] applicationActivities:nil];
        [activity setValue:subjectText forKey:@"subject"];

        // iPad requires an anchor for the popover.
        if (activity.popoverPresentationController != nil)
        {
            activity.popoverPresentationController.sourceView = root.view;
            activity.popoverPresentationController.sourceRect =
                CGRectMake(CGRectGetMidX(root.view.bounds), CGRectGetMidY(root.view.bounds), 0, 0);
            activity.popoverPresentationController.permittedArrowDirections = 0;
        }

        [root presentViewController:activity animated:YES completion:nil];
    });
}

}
