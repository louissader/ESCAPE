#import <UIKit/UIKit.h>

// Transient haptics for ESCAPE. Generators are created per call — cheap at
// gameplay rates and avoids lifetime management across Unity's bridge.
// Unity's scripting main thread is the app main thread on iOS, so UIKit
// calls here are safe. Devices without a Taptic Engine (iPads) no-op.

extern "C" {

void _EscapeHapticImpact(int strength)
{
    UIImpactFeedbackStyle style = UIImpactFeedbackStyleLight;
    if (strength == 1)
        style = UIImpactFeedbackStyleMedium;
    else if (strength >= 2)
        style = UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *gen = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    [gen impactOccurred];
}

void _EscapeHapticNotify(int success)
{
    UINotificationFeedbackGenerator *gen = [[UINotificationFeedbackGenerator alloc] init];
    [gen notificationOccurred:(success
        ? UINotificationFeedbackTypeSuccess
        : UINotificationFeedbackTypeError)];
}

void _EscapeHapticSelect(void)
{
    UISelectionFeedbackGenerator *gen = [[UISelectionFeedbackGenerator alloc] init];
    [gen selectionChanged];
}

}
