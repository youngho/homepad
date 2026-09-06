#import <UIKit/UIKit.h>

extern UIView *UnityGetGLView(void);
extern UIViewController *UnityGetGLViewController(void);

typedef void (*HomepadEdgePanCallback)(int state, float translationX, float translationY, float velocityX);

@interface HomepadEdgePanController : NSObject <UIGestureRecognizerDelegate>
@property (nonatomic, strong) UIScreenEdgePanGestureRecognizer *recognizer;
@property (nonatomic, assign) HomepadEdgePanCallback callback;
@property (nonatomic, assign) int attachTries;
@end

@implementation HomepadEdgePanController

+ (instancetype)shared
{
    static HomepadEdgePanController *instance;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [HomepadEdgePanController new];
    });
    return instance;
}

- (void)install:(HomepadEdgePanCallback)callback
{
    self.callback = callback;
    self.attachTries = 0;
    dispatch_async(dispatch_get_main_queue(), ^{
        [self attach];
    });
}

- (void)attach
{
    if (self.callback == NULL) return;

    UIView *view = nil;
    UIViewController *controller = UnityGetGLViewController();
    if (controller != nil) view = controller.view;
    if (view == nil) view = UnityGetGLView();
    if (view == nil)
    {
        if (self.attachTries++ < 40)
        {
            dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.15 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
                [self attach];
            });
        }
        return;
    }

    if (self.recognizer != nil)
    {
        if (self.recognizer.view == view) return;
        [self.recognizer.view removeGestureRecognizer:self.recognizer];
        self.recognizer = nil;
    }

    UIScreenEdgePanGestureRecognizer *recognizer =
        [[UIScreenEdgePanGestureRecognizer alloc] initWithTarget:self action:@selector(handlePan:)];
    recognizer.edges = UIRectEdgeRight;
    recognizer.delegate = self;
    recognizer.cancelsTouchesInView = YES;
    recognizer.delaysTouchesBegan = NO;
    [view addGestureRecognizer:recognizer];
    self.recognizer = recognizer;
}

- (void)handlePan:(UIScreenEdgePanGestureRecognizer *)recognizer
{
    if (self.callback == NULL) return;

    CGFloat scale = recognizer.view != nil ? recognizer.view.contentScaleFactor : [UIScreen mainScreen].scale;
    CGPoint translation = [recognizer translationInView:recognizer.view];
    CGPoint velocity = [recognizer velocityInView:recognizer.view];
    self.callback(
        (int)recognizer.state,
        (float)(translation.x * scale),
        (float)(translation.y * scale),
        (float)(velocity.x * scale));
}

- (void)uninstall
{
    self.callback = NULL;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (self.recognizer != nil)
        {
            [self.recognizer.view removeGestureRecognizer:self.recognizer];
            self.recognizer = nil;
        }
    });
}

- (BOOL)gestureRecognizerShouldBegin:(UIGestureRecognizer *)gestureRecognizer
{
    return YES;
}

@end

extern "C" {

void Homepad_InstallRightEdgePan(HomepadEdgePanCallback callback)
{
    [[HomepadEdgePanController shared] install:callback];
}

void Homepad_UninstallRightEdgePan(void)
{
    [[HomepadEdgePanController shared] uninstall];
}

}
