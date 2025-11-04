#import <AppKit/AppKit.h>
#include <CoreFoundation/CoreFoundation.h>
#include <CoreGraphics/CoreGraphics.h>
#include <dispatch/dispatch.h>
#include <math.h>
#include <stdlib.h>

#if __has_include(<PDFKit/PDFKit.h>)
#import <PDFKit/PDFKit.h>
#define PRINTINGTOOLS_USE_PDFKIT 1
#else
#define PRINTINGTOOLS_USE_PDFKIT 0
#endif

#ifndef NSPrintSelectionOnly
#define NSPrintSelectionOnly @"NSPrintSelectionOnly"
#endif

#ifndef NSPrintDuplex
#define NSPrintDuplex @"NSPrintDuplex"
#endif

#ifndef NSPrintPanelShowsPrintSelection
#define NSPrintPanelShowsPrintSelection 0
#endif

#ifndef NSPrintColorMode
#define NSPrintColorMode @"NSPrintColorMode"
#endif

#ifndef NSPrintColorModeColor
#define NSPrintColorModeColor @"NSPrintColorModeColor"
#endif

#ifndef NSPrintColorModeGray
#define NSPrintColorModeGray @"NSPrintColorModeGray"
#endif

#ifndef NSPrintColorModeBlackAndWhite
#define NSPrintColorModeBlackAndWhite @"NSPrintColorModeBlackAndWhite"
#endif

#ifndef NSPrintPagesPerSheet
#define NSPrintPagesPerSheet @"NSPrintPagesPerSheet"
#endif

#ifndef NSPrintPagesAcross
#define NSPrintPagesAcross @"NSPrintPagesAcross"
#endif

#ifndef NSPrintPagesDown
#define NSPrintPagesDown @"NSPrintPagesDown"
#endif

#ifndef NSPrintDuplexNone
#define NSPrintDuplexNone 0
#endif

#ifndef NSPrintDuplexLongEdge
#define NSPrintDuplexLongEdge 1
#endif

#ifndef NSPrintDuplexShortEdge
#define NSPrintDuplexShortEdge 2
#endif

#ifndef NSPaperNameLetter
#define NSPaperNameLetter @"Letter"
#endif

#ifndef NSPaperNameLegal
#define NSPaperNameLegal @"Legal"
#endif

#ifndef NSPaperNameA4
#define NSPaperNameA4 @"A4"
#endif

#ifndef NSPaperNameTabloid
#define NSPaperNameTabloid @"Tabloid"
#endif

#ifndef NSPrintOperationSuccessful
#define NSPrintOperationSuccessful @"NSPrintOperationSuccessful"
#endif

#ifndef NSPrintOperationWillRunNotification
#define NSPrintOperationWillRunNotification @"NSPrintOperationWillRunNotification"
#endif

#ifndef NSPrintOperationDidRunNotification
#define NSPrintOperationDidRunNotification @"NSPrintOperationDidRunNotification"
#endif

static const int PrintingToolsJobEventWillRun = 0;
static const int PrintingToolsJobEventCompleted = 1;
static const int PrintingToolsJobEventFailed = 2;
static const int PrintingToolsJobEventCancelled = 3;

@class PrintingToolsOperationHost;
@class PrintingToolsSummaryAccessoryController;
@class PrintingToolsManagedPreviewHost;

@interface NSPrintPanel (PrintingToolsSheet)
- (void)beginSheetModalForWindow:(NSWindow*)window completionHandler:(void (^)(NSModalResponse response))handler;
@end

static void PrintingToolsRunOnMainThread(dispatch_block_t block);
static NSString* PrintingToolsColorModeString(int colorMode);
static int PrintingToolsColorModeFromString(NSString* colorMode);
static NSNumber* PrintingToolsDuplexNumber(int duplexMode);
static int PrintingToolsDuplexFromNumber(NSNumber* value);
static void PrintingToolsApplyPaperPreset(NSPrintInfo* info, NSSize targetSize, PrintingToolsOperationHost* host);
static CGColorSpaceRef PrintingToolsCreateColorSpace(int code);

@interface PrintingToolsPreviewView : NSView
@property (nonatomic, assign) void* managedContext;
@property (nonatomic, assign) NSUInteger pageIndex;
@property (nonatomic, assign) NSUInteger pageCount;
@end

@interface PrintingToolsPdfView : NSView
@property (nonatomic, strong) NSPDFImageRep* pdfRepresentation;
@property (nonatomic, assign) NSInteger currentPage;
@end

typedef struct
{
    void* context;
    void (*renderPage)(void* context, CGContextRef cgContext, NSUInteger pageIndex);
    NSUInteger (*getPageCount)(void* context);
    void (*logDiagnostic)(void* context, const unichar* message, int length);
    void (*jobEvent)(void* context, int eventId, const unichar* message, int messageLength, int errorCode);
} PrintingToolsManagedCallbacks;

typedef struct
{
    double paperWidth;
    double paperHeight;
    double marginLeft;
    double marginTop;
    double marginRight;
    double marginBottom;
    int hasPageRange;
    int fromPage;
    int toPage;
    int orientation;
    int copies;
    int colorMode;
    int duplex;
    int showPrintPanel;
    int showProgressPanel;
    const unichar* jobName;
    int jobNameLength;
    const unichar* printerName;
    int printerNameLength;
    int enablePdfExport;
    const unichar* pdfPath;
    int pdfPathLength;
    int pageCount;
    double dpiX;
    double dpiY;
    int preferredColorSpace;
    int layoutMode;
    int layoutNUpRows;
    int layoutNUpColumns;
    int layoutNUpOrder;
    int layoutBookletBinding;
    int layoutPosterRows;
    int layoutPosterColumns;
} PrintingToolsManagedPrintSettings;

typedef struct
{
    double paperWidth;
    double paperHeight;
    double marginLeft;
    double marginTop;
    double marginRight;
    double marginBottom;
    int orientation;
    int copies;
    int colorMode;
    int duplex;
    int fromPage;
    int toPage;
    int pageRangeEnabled;
    int selectionOnly;
    const unichar* paperName;
    int paperNameLength;
} PrintingToolsManagedPrintInfo;

typedef struct
{
    void* operation;
    int allowsSelection;
    int requestedRangeStart;
    int requestedRangeEnd;
    int requestedCopies;
    const unichar* title;
    int titleLength;
} PrintingToolsPrintPanelOptions;

typedef struct
{
    const unichar** names;
    int* lengths;
    int count;
} PrintingToolsStringArray;

typedef struct
{
    const void* pdfBytes;
    int length;
    int showPrintPanel;
} PrintingToolsVectorDocument;

@interface PrintingToolsOperationHost : NSObject
@property (nonatomic, assign) void* managedContext;
@property (nonatomic, assign) PrintingToolsManagedCallbacks callbacks;
@property (nonatomic, strong) NSPrintOperation* operation;
@property (nonatomic, strong) PrintingToolsPreviewView* previewView;
@property (nonatomic, strong) NSPrintPanel* printPanel;
@property (nonatomic, strong) NSViewController<NSPrintPanelAccessorizing>* accessoryController;
@property (nonatomic, strong) NSString* panelTitle;
@property (nonatomic, assign) NSRange requestedRange;
@property (nonatomic, assign) BOOL allowsSelection;
@property (nonatomic, assign) NSUInteger requestedCopies;
@property (nonatomic, assign) NSInteger panelReturnCode;
@property (nonatomic) dispatch_semaphore_t panelSemaphore;
@property (nonatomic, assign) BOOL selectionOnly;
@property (nonatomic, strong) NSString* lastPaperName;
@property (nonatomic, assign) unichar* cachedPaperName;
@property (nonatomic, assign) double dpiX;
@property (nonatomic, assign) double dpiY;
@property (nonatomic, assign) int preferredColorSpace;
- (instancetype)initWithContext:(void*)context callbacks:(PrintingToolsManagedCallbacks)callbacks;
- (void)emitDiagnostic:(NSString*)message;
- (void)resetPanelState;
- (void)finalizePrintPanelWithResult:(NSInteger)modalResult;
@end

@interface PrintingToolsSummaryAccessoryController : NSViewController <NSPrintPanelAccessorizing>
@property (nonatomic, weak) PrintingToolsOperationHost* host;
@property (nonatomic, strong) NSTextField* label;
- (instancetype)initWithHost:(PrintingToolsOperationHost*)host;
- (void)updateSummary;
@end

@interface PrintingToolsManagedPreviewHost : NSObject
@property (nonatomic, strong) NSView* containerView;
#if PRINTINGTOOLS_USE_PDFKIT
@property (nonatomic, strong) PDFView* pdfView;
#else
@property (nonatomic, strong) PrintingToolsPdfView* fallbackPdfView;
#endif
- (void)updateWithPdfData:(NSData*)data dpi:(double)dpi;
- (void)resetLayout;
@end

@implementation PrintingToolsPreviewView
- (BOOL)isFlipped
{
    return YES;
}

- (void)drawRect:(NSRect)dirtyRect
{
    [[NSColor whiteColor] setFill];
    NSRectFill(dirtyRect);

    if (self.managedContext == NULL)
    {
        return;
    }

    PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)self.managedContext;
    if (host == nil)
    {
        return;
    }

    if (host.callbacks.renderPage == NULL || self.pageIndex >= self.pageCount)
    {
        return;
    }

    NSUInteger renderIndex = self.pageIndex;
    NSPrintOperation* operation = [NSPrintOperation currentOperation];
    if (operation != nil)
    {
        NSInteger currentPage = operation.currentPage;
        if (currentPage > 0)
        {
            NSUInteger candidate = (NSUInteger)(currentPage - 1);
            if (candidate < self.pageCount)
            {
                renderIndex = candidate;
            }
        }
    }

    NSLog(@"[PrintingTools] PreviewView drawing page %lu (renderIndex=%lu)", (unsigned long)self.pageIndex, (unsigned long)renderIndex);

    CGContextRef context = [[NSGraphicsContext currentContext] CGContext];
    host.callbacks.renderPage(host.callbacks.context, context, renderIndex);
}

- (BOOL)knowsPageRange:(NSRangePointer)range
{
    if (self.pageCount == 0)
    {
        return NO;
    }

    range->location = 1;
    range->length = self.pageCount;
    return YES;
}

- (NSRect)rectForPage:(NSInteger)page
{
    if (page < 1 || (NSUInteger)page > self.pageCount)
    {
        return NSZeroRect;
    }

    self.pageIndex = (NSUInteger)(page - 1);
    [self setNeedsDisplay:YES];
    return self.bounds;
}
@end

@implementation PrintingToolsPdfView

- (instancetype)initWithFrame:(NSRect)frameRect
{
    self = [super initWithFrame:frameRect];
    if (self)
    {
        _currentPage = 0;
    }
    return self;
}

- (void)drawRect:(NSRect)dirtyRect
{
    [[NSColor whiteColor] setFill];
    NSRectFillUsingOperation(dirtyRect, NSCompositingOperationSourceOver);

    if (self.pdfRepresentation == nil)
    {
        return;
    }

    NSInteger renderPage = self.currentPage;
    if (renderPage < 0)
    {
        renderPage = 0;
    }
    else if (self.pdfRepresentation.pageCount > 0 && renderPage >= self.pdfRepresentation.pageCount)
    {
        renderPage = self.pdfRepresentation.pageCount - 1;
    }

    self.currentPage = renderPage;
    NSLog(@"[PrintingTools] PdfView drawing page %ld", (long)renderPage);
    self.pdfRepresentation.currentPage = renderPage;
    [self.pdfRepresentation drawInRect:self.bounds];
}

- (BOOL)knowsPageRange:(NSRangePointer)range
{
    if (self.pdfRepresentation == nil)
    {
        return NO;
    }

    NSInteger pageTotal = self.pdfRepresentation.pageCount;
    if (pageTotal <= 0)
    {
        return NO;
    }

    range->location = 1;
    range->length = (NSUInteger)pageTotal;
    return YES;
}

- (NSRect)rectForPage:(NSInteger)page
{
    if (self.pdfRepresentation == nil)
    {
        return NSZeroRect;
    }

    if (page < 1 || page > self.pdfRepresentation.pageCount)
    {
        return NSZeroRect;
    }

    self.currentPage = page - 1;
    self.pdfRepresentation.currentPage = self.currentPage;
    NSRect pdfBounds = self.pdfRepresentation.bounds;
    NSRect frame = NSMakeRect(0, 0, pdfBounds.size.width, pdfBounds.size.height);
    [self setFrame:frame];
    [self setNeedsDisplay:YES];
    return pdfBounds;
}

@end



@implementation PrintingToolsOperationHost

- (instancetype)initWithContext:(void*)context callbacks:(PrintingToolsManagedCallbacks)callbacks
{
    self = [super init];
    if (self)
    {
        _managedContext = context;
        _callbacks = callbacks;

        NSPrintInfo* sharedInfo = [NSPrintInfo sharedPrintInfo];
        NSPrintInfo* info = [[NSPrintInfo alloc] initWithDictionary:[sharedInfo dictionary]];

        _previewView = [[PrintingToolsPreviewView alloc] initWithFrame:NSMakeRect(0, 0, info.paperSize.width, info.paperSize.height)];
        _previewView.managedContext = (__bridge void*)self;
        _previewView.pageIndex = 0;

        _operation = [NSPrintOperation printOperationWithView:_previewView printInfo:info];
        _operation.showsPrintPanel = YES;
        _operation.showsProgressPanel = YES;
        _printPanel = _operation.printPanel;
        _printPanel.options = (NSPrintPanelOptions)(
            NSPrintPanelShowsPaperSize |
            NSPrintPanelShowsOrientation |
            NSPrintPanelShowsPreview |
            NSPrintPanelShowsCopies |
            NSPrintPanelShowsPageRange |
            NSPrintPanelShowsScaling |
            NSPrintPanelShowsPrintSelection);

        NSNotificationCenter* center = [NSNotificationCenter defaultCenter];
        [center addObserver:self selector:@selector(handleOperationWillRun:) name:NSPrintOperationWillRunNotification object:_operation];
        [center addObserver:self selector:@selector(handleOperationDidRun:) name:NSPrintOperationDidRunNotification object:_operation];

        _panelTitle = nil;
        _requestedRange = NSMakeRange(1, 0);
        _requestedCopies = 1;
        _allowsSelection = NO;
        _panelReturnCode = NSModalResponseCancel;
        _panelSemaphore = NULL;
        _selectionOnly = NO;
        _lastPaperName = info.paperName;
        _dpiX = 300.0;
        _dpiY = 300.0;
        _preferredColorSpace = 0;
    }
    return self;
}

- (void)resetPanelState
{
    _panelReturnCode = NSModalResponseCancel;
    _selectionOnly = NO;
    _requestedRange = NSMakeRange(1, 0);
    _requestedCopies = 1;
}

- (BOOL)preparePrintPanelWithOptions:(const PrintingToolsPrintPanelOptions*)options
{
    if (options == NULL)
    {
        return NO;
    }

    if (self.operation == nil)
    {
        return NO;
    }

    NSPrintInfo* info = self.operation.printInfo;
    if (info == nil)
    {
        return NO;
    }

    [self resetPanelState];

    self.allowsSelection = options->allowsSelection != 0;
    self.requestedCopies = options->requestedCopies > 0 ? (NSUInteger)options->requestedCopies : 1;

    if (options->title != NULL && options->titleLength > 0)
    {
        self.panelTitle = [[NSString alloc] initWithCharacters:options->title length:options->titleLength];
    }
    else
    {
        self.panelTitle = nil;
    }

    if (options->requestedRangeStart > 0 && options->requestedRangeEnd >= options->requestedRangeStart)
    {
        NSUInteger length = (NSUInteger)(options->requestedRangeEnd - options->requestedRangeStart + 1);
        self.requestedRange = NSMakeRange((NSUInteger)options->requestedRangeStart, length);
    }
    else
    {
        self.requestedRange = NSMakeRange(1, 0);
    }

    NSMutableDictionary<NSPrintInfoAttributeKey, id>* attributes = info.dictionary;
    if (attributes != nil)
    {
        attributes[NSPrintCopies] = @(self.requestedCopies);
        if (self.requestedRange.length > 0)
        {
            attributes[NSPrintAllPages] = @(NO);
            attributes[NSPrintFirstPage] = @(self.requestedRange.location);
            attributes[NSPrintLastPage] = @(NSMaxRange(self.requestedRange) - 1);
        }
        else
        {
            attributes[NSPrintAllPages] = @(YES);
            [attributes removeObjectForKey:NSPrintFirstPage];
            [attributes removeObjectForKey:NSPrintLastPage];
        }

        if (!self.allowsSelection)
        {
            [attributes removeObjectForKey:NSPrintSelectionOnly];
        }
    }

    NSPrintPanelOptions baseOptions = (NSPrintPanelOptions)(
        NSPrintPanelShowsPaperSize |
        NSPrintPanelShowsOrientation |
        NSPrintPanelShowsPreview |
        NSPrintPanelShowsCopies |
        NSPrintPanelShowsPageRange |
        NSPrintPanelShowsScaling);

    if (self.allowsSelection)
    {
        baseOptions |= NSPrintPanelShowsPrintSelection;
    }
    else
    {
        baseOptions &= ~NSPrintPanelShowsPrintSelection;
    }

    self.printPanel.options = baseOptions;

    if (self.accessoryController == nil)
    {
        self.accessoryController = [[PrintingToolsSummaryAccessoryController alloc] initWithHost:self];
    }

    NSArray<NSViewController<NSPrintPanelAccessorizing>*>* existingControllers = self.printPanel.accessoryControllers;
    if (existingControllers != nil)
    {
        for (NSViewController<NSPrintPanelAccessorizing>* controller in existingControllers)
        {
            [self.printPanel removeAccessoryController:controller];
        }
    }

    [self.printPanel addAccessoryController:self.accessoryController];
    return YES;
}

- (void)finalizePrintPanelWithResult:(NSInteger)modalResult
{
    self.panelReturnCode = modalResult;

    if (self.operation == nil)
    {
        return;
    }

    NSPrintInfo* info = self.operation.printInfo;
    if (info == nil)
    {
        return;
    }

    NSMutableDictionary<NSPrintInfoAttributeKey, id>* attributes = info.dictionary;
    if (attributes != nil)
    {
        NSNumber* selection = attributes[NSPrintSelectionOnly];
        self.selectionOnly = selection != nil && selection.boolValue;

        NSNumber* first = attributes[NSPrintFirstPage];
        NSNumber* last = attributes[NSPrintLastPage];
        if (first != nil && last != nil)
        {
            NSUInteger start = MAX(1, first.unsignedIntegerValue);
            NSUInteger end = MAX(start, last.unsignedIntegerValue);
            self.requestedRange = NSMakeRange(start, end - start + 1);
        }
        else
        {
            self.requestedRange = NSMakeRange(1, 0);
        }

        NSNumber* copiesValue = attributes[NSPrintCopies];
        if (copiesValue != nil)
        {
            self.requestedCopies = MAX(1, copiesValue.unsignedIntegerValue);
        }
    }

    NSString* logMessage = [NSString stringWithFormat:@"Print panel completed (result=%ld) range=%@ copies=%lu selection=%@",
                                                     (long)modalResult,
                                                     NSStringFromRange(self.requestedRange),
                                                     (unsigned long)self.requestedCopies,
                                                     self.selectionOnly ? @"YES" : @"NO"];
    [self emitDiagnostic:logMessage];
}

- (void)emitDiagnostic:(NSString*)message
{
    if (message == nil || message.length == 0)
    {
        return;
    }

    NSLog(@"[PrintingTools] %@", message);

    if (_callbacks.logDiagnostic == NULL)
    {
        return;
    }

    NSUInteger length = message.length;
    unichar* temp = (unichar*)calloc(length, sizeof(unichar));
    if (temp == NULL)
    {
        return;
    }

    [message getCharacters:temp range:NSMakeRange(0, length)];
    _callbacks.logDiagnostic(_callbacks.context, temp, (int)length);
    free(temp);
}

- (void)handleOperationWillRun:(NSNotification*)notification
{
    NSPrintOperation* printOperation = notification.object;
    NSString* title = printOperation.jobTitle ?: @"Untitled Print Job";
    NSString* message = [NSString stringWithFormat:@"macOS print job '%@' will start.", title];
    [self notifyJobEvent:PrintingToolsJobEventWillRun message:message error:nil];
}

- (void)handleOperationDidRun:(NSNotification*)notification
{
    NSPrintOperation* printOperation = notification.object;
    NSDictionary* userInfo = notification.userInfo;
    BOOL success = YES;
    if (userInfo != nil)
    {
        NSNumber* value = userInfo[NSPrintOperationSuccessful];
        if (value != nil)
        {
            success = value.boolValue;
        }
    }

    NSString* title = printOperation.jobTitle ?: @"Untitled Print Job";
    if (success)
    {
        NSString* message = [NSString stringWithFormat:@"macOS print job '%@' completed successfully.", title];
        [self notifyJobEvent:PrintingToolsJobEventCompleted message:message error:nil];
    }
    else
    {
        NSString* message = [NSString stringWithFormat:@"macOS print job '%@' was cancelled.", title];
        [self notifyJobEvent:PrintingToolsJobEventCancelled message:message error:nil];
    }
}

- (void)notifyJobEvent:(int)eventKind message:(NSString*)message error:(NSError*)error
{
    if (message != nil)
    {
        [self emitDiagnostic:message];
    }

    if (_callbacks.jobEvent == NULL)
    {
        return;
    }

    const unichar* direct = NULL;
    unichar* allocated = NULL;
    int length = 0;
    if (message != nil)
    {
        length = (int)message.length;
        if (length > 0)
        {
            direct = CFStringGetCharactersPtr((CFStringRef)message);
            if (direct == NULL)
            {
                allocated = (unichar*)malloc(sizeof(unichar) * length);
                if (allocated != NULL)
                {
                    [message getCharacters:allocated range:NSMakeRange(0, length)];
                    direct = allocated;
                }
                else
                {
                    length = 0;
                }
            }
        }
    }

    int errorCode = error != nil ? (int)error.code : 0;
    _callbacks.jobEvent(_callbacks.context, eventKind, direct, length, errorCode);

    if (allocated != NULL)
    {
        free(allocated);
    }
}

@end

@implementation PrintingToolsSummaryAccessoryController

- (instancetype)initWithHost:(PrintingToolsOperationHost*)host
{
    self = [super initWithNibName:nil bundle:nil];
    if (self)
    {
        _host = host;
    }
    return self;
}

- (void)loadView
{
    NSView* container = [[NSView alloc] initWithFrame:NSMakeRect(0, 0, 240, 56)];
    container.translatesAutoresizingMaskIntoConstraints = NO;

    NSTextField* label = [[NSTextField alloc] initWithFrame:NSMakeRect(0, 0, 240, 56)];
    label.editable = NO;
    label.bezeled = NO;
    label.drawsBackground = NO;
    label.lineBreakMode = NSLineBreakByWordWrapping;
    label.font = [NSFont systemFontOfSize:11.0];
    label.stringValue = @"";
    label.translatesAutoresizingMaskIntoConstraints = NO;

    [container addSubview:label];

    [NSLayoutConstraint activateConstraints:@[
        [label.leadingAnchor constraintEqualToAnchor:container.leadingAnchor],
        [label.trailingAnchor constraintEqualToAnchor:container.trailingAnchor],
        [label.topAnchor constraintEqualToAnchor:container.topAnchor],
        [label.bottomAnchor constraintEqualToAnchor:container.bottomAnchor]
    ]];

    self.view = container;
    self.label = label;
    self.preferredContentSize = NSMakeSize(240, 56);
}

- (void)viewWillAppear
{
    [super viewWillAppear];
    [self updateSummary];
}

- (NSArray<NSString*>*)keyPathsForValuesAffectingPreview
{
    return @[];
}

- (NSArray<NSString*>*)localizedSummaryItems
{
    [self updateSummary];
    if (self.label.stringValue.length == 0)
    {
        return @[];
    }

    return @[ self.label.stringValue ];
}

- (void)updateSummary
{
    if (self.host == nil || self.host.operation == nil)
    {
        self.label.stringValue = @"";
        return;
    }

    NSPrintInfo* info = self.host.operation.printInfo;
    if (info == nil)
    {
        self.label.stringValue = @"";
        return;
    }

    NSString* paperName = info.paperName;
    if (paperName == nil || paperName.length == 0)
    {
        paperName = self.host.lastPaperName ?: @"Custom";
    }

    double width = info.paperSize.width;
    double height = info.paperSize.height;

    NSUInteger copies = 1;
    NSNumber* copiesValue = info.dictionary[NSPrintCopies];
    if (copiesValue != nil)
    {
        copies = MAX(1, copiesValue.unsignedIntegerValue);
    }

    NSNumber* duplexValue = info.dictionary[NSPrintDuplex];
    NSString* duplexString = duplexValue != nil ? [NSString stringWithFormat:@"Duplex=%@", duplexValue] : @"1-sided";

    NSString* summary = [NSString stringWithFormat:@"%@ — %.0f × %.0f pt — %lu copies — %@",
                                                   paperName,
                                                   width,
                                                   height,
                                                   (unsigned long)copies,
                                                   duplexString];
    self.label.stringValue = summary;
}

@end

@implementation PrintingToolsManagedPreviewHost

- (instancetype)init
{
    self = [super init];
    if (self)
    {
        NSScrollView* scrollView = [[NSScrollView alloc] initWithFrame:NSMakeRect(0, 0, 640, 480)];
        scrollView.translatesAutoresizingMaskIntoConstraints = NO;
        scrollView.hasVerticalScroller = YES;
        scrollView.hasHorizontalScroller = YES;
        scrollView.borderType = NSNoBorder;

#if PRINTINGTOOLS_USE_PDFKIT
        PDFView* pdfView = [[PDFView alloc] initWithFrame:scrollView.bounds];
        pdfView.autoScales = YES;
        pdfView.displayMode = kPDFDisplaySinglePageContinuous;
        pdfView.backgroundColor = [NSColor colorWithCalibratedWhite:0.95 alpha:1.0];
        scrollView.documentView = pdfView;
        _pdfView = pdfView;
#else
        PrintingToolsPdfView* pdfView = [[PrintingToolsPdfView alloc] initWithFrame:scrollView.bounds];
        scrollView.documentView = pdfView;
        _fallbackPdfView = pdfView;
#endif

        _containerView = scrollView;
    }
    return self;
}

- (void)updateWithPdfData:(NSData*)data dpi:(double)dpi
{
    if (data == nil || data.length == 0)
    {
        return;
    }

#if PRINTINGTOOLS_USE_PDFKIT
    PDFDocument* document = [[PDFDocument alloc] initWithData:data];
    if (document == nil)
    {
        return;
    }

    self.pdfView.document = document;
    if (dpi > 0)
    {
        double scale = dpi / 72.0;
        self.pdfView.scaleFactor = scale;
    }
    [self.pdfView layoutDocumentView];
#else
    NSPDFImageRep* rep = [NSPDFImageRep imageRepWithData:data];
    if (rep == nil)
    {
        return;
    }

    self.fallbackPdfView.pdfRepresentation = rep;
    self.fallbackPdfView.currentPage = 0;
    NSRect bounds = rep.bounds;
    [self.fallbackPdfView setFrame:bounds];
    [self.fallbackPdfView setNeedsDisplay:YES];
#endif
}

- (void)resetLayout
{
#if PRINTINGTOOLS_USE_PDFKIT
    [self.pdfView layoutDocumentView];
#else
    if (self.fallbackPdfView.pdfRepresentation != nil)
    {
        [self.fallbackPdfView setNeedsDisplay:YES];
    }
#endif
}

@end

void* PrintingTools_CreatePrintOperation(void* context)
{
    PrintingToolsManagedCallbacks callbacks = {0};
    if (context != NULL)
    {
        callbacks = *((PrintingToolsManagedCallbacks*)context);
    }

    PrintingToolsOperationHost* host = [[PrintingToolsOperationHost alloc] initWithContext:context callbacks:callbacks];
    if (host == nil)
    {
        return NULL;
    }

    // Managed code receives ownership and must call PrintingTools_DisposePrintOperation.
    return (__bridge_retained void*)host;
}

void PrintingTools_DisposePrintOperation(void* operation)
{
    if (operation == NULL)
    {
        return;
    }

    PrintingToolsOperationHost* host = (__bridge_transfer PrintingToolsOperationHost*)operation;
    [[NSNotificationCenter defaultCenter] removeObserver:host];
    if (host.cachedPaperName != NULL)
    {
        free(host.cachedPaperName);
        host.cachedPaperName = NULL;
    }
    if (host.printPanel != nil && host.accessoryController != nil)
    {
        [host.printPanel removeAccessoryController:host.accessoryController];
    }
    host.operation = nil;
    host.previewView = nil;
    host.printPanel = nil;
    host.accessoryController = nil;
    host.panelTitle = nil;
    host.lastPaperName = nil;
}

void PrintingTools_ConfigurePrintOperation(void* operation, const PrintingToolsManagedPrintSettings* settings)
{
    if (operation == NULL || settings == NULL)
    {
        return;
    }

    PrintingToolsRunOnMainThread(^{
        PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
        if (host.operation == nil)
        {
            return;
        }

        NSPrintInfo* info = host.operation.printInfo;
        if (info == nil)
        {
            return;
        }

        NSMutableDictionary<NSPrintInfoAttributeKey, id>* attributes = info.dictionary;

        NSSize targetSize = NSMakeSize(settings->paperWidth, settings->paperHeight);
        PrintingToolsApplyPaperPreset(info, targetSize, host);

        info.leftMargin = settings->marginLeft;
        info.topMargin = settings->marginTop;
        info.rightMargin = settings->marginRight;
        info.bottomMargin = settings->marginBottom;

        info.orientation = settings->orientation == 1 ? NSPaperOrientationLandscape : NSPaperOrientationPortrait;

        if (attributes != nil)
        {
            attributes[@"com.avalonia.layout.kind"] = @(settings->layoutMode);

            if (settings->hasPageRange)
            {
                attributes[NSPrintAllPages] = @(NO);
                attributes[NSPrintFirstPage] = @(MAX(1, settings->fromPage));
                attributes[NSPrintLastPage] = @(MAX(settings->fromPage, settings->toPage));
            }
            else
            {
                attributes[NSPrintAllPages] = @(YES);
                [attributes removeObjectForKey:NSPrintFirstPage];
                [attributes removeObjectForKey:NSPrintLastPage];
            }

            attributes[NSPrintCopies] = @(MAX(1, settings->copies));

            NSString* colorMode = PrintingToolsColorModeString(settings->colorMode);
            if (colorMode != nil)
            {
                attributes[NSPrintColorMode] = colorMode;
            }
            else
            {
                [attributes removeObjectForKey:NSPrintColorMode];
            }

            attributes[NSPrintDuplex] = PrintingToolsDuplexNumber(settings->duplex);

            switch (settings->layoutMode)
            {
                case 1:
                {
                    int rows = MAX(1, settings->layoutNUpRows);
                    int columns = MAX(1, settings->layoutNUpColumns);
                    int pagesPerSheet = MAX(1, MIN(16, rows * columns));
                    attributes[NSPrintPagesPerSheet] = @(pagesPerSheet);
                    attributes[@"NSPrintPagesAcross"] = @(columns);
                    attributes[@"NSPrintPagesDown"] = @(rows);
                    break;
                }
                case 2:
                {
                    // Ensure duplex binding reflects booklet preference.
                    attributes[NSPrintDuplex] = settings->layoutBookletBinding != 0
                        ? @(NSPrintDuplexLongEdge)
                        : @(NSPrintDuplexShortEdge);
                    attributes[@"NSPrintPagesAcross"] = @(1);
                    attributes[@"NSPrintPagesDown"] = @(1);
                    break;
                }
                case 3:
                {
                    int rows = MAX(1, settings->layoutPosterRows);
                    int columns = MAX(1, settings->layoutPosterColumns);
                    int tiles = MAX(1, rows * columns);
                    attributes[NSPrintPagesPerSheet] = @(tiles);
                    attributes[@"NSPrintPagesAcross"] = @(columns);
                    attributes[@"NSPrintPagesDown"] = @(rows);
                    break;
                }
                default:
                {
                    attributes[NSPrintPagesPerSheet] = @(1);
                    [attributes removeObjectForKey:@"NSPrintPagesAcross"];
                    [attributes removeObjectForKey:@"NSPrintPagesDown"];
                    break;
                }
            }
        }

        if (settings->enablePdfExport && settings->pdfPath != NULL && settings->pdfPathLength > 0)
        {
            NSString* pdfPath = [[NSString alloc] initWithCharacters:settings->pdfPath length:settings->pdfPathLength];
            NSURL* saveURL = [NSURL fileURLWithPath:pdfPath];
            if (saveURL != nil)
            {
                info.jobDisposition = NSPrintSaveJob;
                if (attributes != nil)
                {
                    attributes[NSPrintJobSavingURL] = saveURL;
                }
                [[NSFileManager defaultManager] removeItemAtURL:saveURL error:nil];
            }
#if !__has_feature(objc_arc)
            [pdfPath release];
#endif
        }
        else
        {
            info.jobDisposition = NSPrintSpoolJob;
            if (attributes != nil)
            {
                [attributes removeObjectForKey:NSPrintJobSavingURL];
            }
        }

        host.operation.showsPrintPanel = settings->showPrintPanel != 0;
        host.operation.showsProgressPanel = settings->showProgressPanel != 0;
        if (settings->dpiX > 0)
        {
            host.dpiX = settings->dpiX;
        }
        if (settings->dpiY > 0)
        {
            host.dpiY = settings->dpiY;
        }
        host.preferredColorSpace = settings->preferredColorSpace;

        if (settings->jobName != NULL && settings->jobNameLength > 0)
        {
            NSString* jobName = [[NSString alloc] initWithCharacters:settings->jobName length:settings->jobNameLength];
            host.operation.jobTitle = jobName;
#if !__has_feature(objc_arc)
            [jobName release];
#endif
        }

        if (settings->printerName != NULL && settings->printerNameLength > 0)
        {
            NSString* printerName = [[NSString alloc] initWithCharacters:settings->printerName length:settings->printerNameLength];
            NSPrinter* printer = [NSPrinter printerWithName:printerName];
            if (printer != nil)
            {
                info.printer = printer;
            }
#if !__has_feature(objc_arc)
            [printerName release];
#endif
        }

        NSUInteger configuredPageCount = settings->pageCount > 0 ? (NSUInteger)settings->pageCount : 1;
        host.previewView.pageCount = configuredPageCount;
        if (host.previewView.pageIndex >= configuredPageCount)
        {
            host.previewView.pageIndex = configuredPageCount - 1;
        }
        host.previewView.frame = NSMakeRect(0, 0, info.paperSize.width, info.paperSize.height);
        [host.previewView setNeedsDisplay:YES];

        NSString* layoutMessage = [NSString stringWithFormat:@"macOS layout applied: kind=%d nUp=%d×%d poster=%d×%d",
                                   settings->layoutMode,
                                   settings->layoutNUpRows,
                                   settings->layoutNUpColumns,
                                   settings->layoutPosterRows,
                                   settings->layoutPosterColumns];
        [host emitDiagnostic:layoutMessage];
    });
}

int PrintingTools_ShowPrintPanel(void* operation, const PrintingToolsPrintPanelOptions* options)
{
    if (operation == NULL || options == NULL)
    {
        return 0;
    }

    __block NSInteger modalResult = NSModalResponseCancel;

    PrintingToolsRunOnMainThread(^{
        PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
        if (![host preparePrintPanelWithOptions:options])
        {
            return;
        }

        NSPrintInfo* info = host.operation.printInfo;
        modalResult = [host.printPanel runModalWithPrintInfo:info];
        [host finalizePrintPanelWithResult:modalResult];
    });

    return modalResult == NSModalResponseOK ? 1 : 0;
}

int PrintingTools_ShowPrintPanelSheet(void* operation, void* windowPtr, const PrintingToolsPrintPanelOptions* options)
{
    if (operation == NULL || windowPtr == NULL || options == NULL)
    {
        return 0;
    }

    __block NSInteger modalResult = NSModalResponseCancel;
    dispatch_semaphore_t semaphore = dispatch_semaphore_create(0);

    PrintingToolsRunOnMainThread(^{
        PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
        NSWindow* window = (__bridge NSWindow*)windowPtr;
        if (host == nil || window == nil)
        {
            dispatch_semaphore_signal(semaphore);
            return;
        }

        if (![host preparePrintPanelWithOptions:options])
        {
            dispatch_semaphore_signal(semaphore);
            return;
        }

        host.panelSemaphore = semaphore;
        [host.printPanel beginSheetModalForWindow:window completionHandler:^(NSModalResponse response) {
            modalResult = response;
            [host finalizePrintPanelWithResult:response];

            dispatch_semaphore_t pending = host.panelSemaphore;
            host.panelSemaphore = NULL;
            if (pending != NULL)
            {
                dispatch_semaphore_signal(pending);
            }
        }];
    });

    dispatch_semaphore_wait(semaphore, DISPATCH_TIME_FOREVER);
    return modalResult == NSModalResponseOK ? 1 : 0;
}

int PrintingTools_ShowPageLayout(void* operation, const PrintingToolsPrintPanelOptions* options)
{
    if (operation == NULL)
    {
        return 0;
    }

    __block NSInteger modalResult = NSModalResponseCancel;

    PrintingToolsRunOnMainThread(^{
        PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
        if (host.operation == nil)
        {
            return;
        }

        NSPrintInfo* info = host.operation.printInfo;
        if (info == nil)
        {
            return;
        }

        NSPageLayout* layout = [NSPageLayout pageLayout];
        modalResult = [layout runModalWithPrintInfo:info];

        if (modalResult == NSModalResponseOK)
        {
            host.lastPaperName = info.paperName;
            NSString* message = [NSString stringWithFormat:@"Page layout confirmed: %@ (%.2f x %.2f)",
                                                           info.paperName,
                                                           info.paperSize.width,
                                                           info.paperSize.height];
            [host emitDiagnostic:message];
        }
    });

    return modalResult == NSModalResponseOK ? 1 : 0;
}

int PrintingTools_GetPrintInfo(void* operation, PrintingToolsManagedPrintInfo* info)
{
    if (operation == NULL || info == NULL)
    {
        return 0;
    }

    __block PrintingToolsManagedPrintInfo captured = {0};
    __block BOOL success = NO;

    PrintingToolsRunOnMainThread(^{
        PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
        if (host.operation == nil)
        {
            return;
        }

        NSPrintInfo* printInfo = host.operation.printInfo;
        if (printInfo == nil)
        {
            return;
        }

        captured.paperWidth = printInfo.paperSize.width;
        captured.paperHeight = printInfo.paperSize.height;
        captured.marginLeft = printInfo.leftMargin;
        captured.marginTop = printInfo.topMargin;
        captured.marginRight = printInfo.rightMargin;
        captured.marginBottom = printInfo.bottomMargin;
        captured.orientation = printInfo.orientation == NSPaperOrientationLandscape ? 1 : 0;

        NSMutableDictionary<NSPrintInfoAttributeKey, id>* attributes = printInfo.dictionary;
        NSNumber* copies = attributes != nil ? attributes[NSPrintCopies] : nil;
        captured.copies = copies != nil ? MAX(1, copies.intValue) : 1;

        NSString* colorMode = attributes != nil ? attributes[NSPrintColorMode] : nil;
        captured.colorMode = PrintingToolsColorModeFromString(colorMode);

        NSNumber* duplexValue = attributes != nil ? attributes[NSPrintDuplex] : nil;
        captured.duplex = PrintingToolsDuplexFromNumber(duplexValue);

        NSNumber* allPages = attributes != nil ? attributes[NSPrintAllPages] : nil;
        BOOL hasRange = !(allPages == nil || allPages.boolValue);
        if (hasRange)
        {
            NSNumber* first = attributes[NSPrintFirstPage];
            NSNumber* last = attributes[NSPrintLastPage];
            captured.fromPage = first != nil ? first.intValue : 1;
            captured.toPage = last != nil ? last.intValue : captured.fromPage;
            captured.pageRangeEnabled = 1;
        }
        else
        {
            captured.pageRangeEnabled = 0;
            captured.fromPage = 1;
            captured.toPage = 1;
        }

        NSNumber* selection = attributes != nil ? attributes[NSPrintSelectionOnly] : nil;
        captured.selectionOnly = selection != nil && selection.boolValue ? 1 : 0;

        NSString* paperName = printInfo.paperName;
        if (paperName == nil || paperName.length == 0)
        {
            paperName = host.lastPaperName;
        }

        if (paperName != nil && paperName.length > 0)
        {
            host.lastPaperName = paperName;
            const unichar* namePtr = CFStringGetCharactersPtr((CFStringRef)paperName);
            if (namePtr == NULL)
            {
                NSUInteger length = paperName.length;
                if (length > 0)
                {
                    if (host.cachedPaperName != NULL)
                    {
                        free(host.cachedPaperName);
                        host.cachedPaperName = NULL;
                    }

                    host.cachedPaperName = (unichar*)malloc(sizeof(unichar) * length);
                    if (host.cachedPaperName != NULL)
                    {
                        [paperName getCharacters:host.cachedPaperName range:NSMakeRange(0, length)];
                        namePtr = host.cachedPaperName;
                    }
                }
            }

            captured.paperName = namePtr;
            captured.paperNameLength = namePtr != NULL ? (int)paperName.length : 0;
        }

        success = YES;
    });

    if (!success)
    {
        return 0;
    }

    *info = captured;
    return 1;
}

void* PrintingTools_GetPreviewView(void* operation)
{
    if (operation == NULL)
    {
        return NULL;
    }

    __block NSView* result = nil;
    PrintingToolsRunOnMainThread(^{
        PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
        if (host.previewView != nil)
        {
            result = host.previewView;
        }
    });

    return (__bridge void*)result;
}

void PrintingTools_BeginPreview(void* operation)
{
    if (operation == NULL)
    {
        return;
    }

    PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
    if (host.operation == nil)
    {
        return;
    }

    NSUInteger pageCount = host.callbacks.getPageCount ? host.callbacks.getPageCount(host.callbacks.context) : 0;
    if (pageCount > 0)
    {
        host.previewView.pageCount = pageCount;
        host.previewView.pageIndex = 0;
    }

    (void)host;
}

int PrintingTools_CommitPrint(void* operation)
{
    if (operation == NULL)
    {
        return 0;
    }

    PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
    if (host.operation == nil)
    {
        return 0;
    }

    NSUInteger pageCount = host.callbacks.getPageCount ? host.callbacks.getPageCount(host.callbacks.context) : 0;
    if (pageCount > 0)
    {
        host.previewView.pageCount = pageCount;
        host.previewView.pageIndex = MIN(host.previewView.pageIndex, pageCount - 1);
    }
    else
    {
        host.previewView.pageCount = 1;
        host.previewView.pageIndex = 0;
    }

    return [host.operation runOperation] ? 1 : 0;
}

int PrintingTools_RunModalPrintOperation(void* operation)
{
    if (operation == NULL)
    {
        return 0;
    }

    PrintingToolsOperationHost* host = (__bridge PrintingToolsOperationHost*)operation;
    if (host.operation == nil)
    {
        return 0;
    }

    NSUInteger pageCount = host.callbacks.getPageCount ? host.callbacks.getPageCount(host.callbacks.context) : 0;
    if (pageCount > 0)
    {
        host.previewView.pageCount = pageCount;
        host.previewView.pageIndex = 0;
    }
    else
    {
        host.previewView.pageCount = 1;
        host.previewView.pageIndex = 0;
    }

    return [host.operation runOperation] ? 1 : 0;
}

int PrintingTools_RunPdfPrintOperation(const void* pdfData, int length, int showPrintPanel)
{
    if (pdfData == NULL || length <= 0)
    {
        return 0;
    }

    NSData* data = [NSData dataWithBytes:pdfData length:length];
    if (data == nil)
    {
        return 0;
    }

    NSPDFImageRep* pdfRepresentation = [NSPDFImageRep imageRepWithData:data];
    if (pdfRepresentation == nil || pdfRepresentation.pageCount <= 0)
    {
        return 0;
    }

    NSPrintInfo* sharedInfo = [NSPrintInfo sharedPrintInfo];
    NSPrintInfo* info = [[NSPrintInfo alloc] initWithDictionary:[sharedInfo dictionary]];
    info.paperSize = pdfRepresentation.bounds.size;

    PrintingToolsPdfView* pdfView = [[PrintingToolsPdfView alloc] initWithFrame:pdfRepresentation.bounds];
    pdfView.pdfRepresentation = pdfRepresentation;
    pdfView.currentPage = 0;

    NSPrintOperation* operation = [NSPrintOperation printOperationWithView:pdfView printInfo:info];
   operation.showsPrintPanel = showPrintPanel != 0;
   operation.showsProgressPanel = showPrintPanel != 0;

   return [operation runOperation] ? 1 : 0;
}

void* PrintingTools_CreateHostWindow(double width, double height)
{
    __block NSWindow* window = nil;
    PrintingToolsRunOnMainThread(^{
        NSRect frame = NSMakeRect(0, 0, width > 0 ? width : 640.0, height > 0 ? height : 480.0);
        NSWindowStyleMask mask = NSWindowStyleMaskTitled |
            NSWindowStyleMaskClosable |
            NSWindowStyleMaskMiniaturizable |
            NSWindowStyleMaskResizable;
        window = [[NSWindow alloc] initWithContentRect:frame styleMask:mask backing:NSBackingStoreBuffered defer:NO];
        [window center];
    });

    return (__bridge_retained void*)window;
}

void PrintingTools_ShowWindow(void* windowPtr)
{
    if (windowPtr == NULL)
    {
        return;
    }

    PrintingToolsRunOnMainThread(^{
        NSWindow* window = (__bridge NSWindow*)windowPtr;
        if (window != nil)
        {
            [window makeKeyAndOrderFront:nil];
        }
    });
}

void PrintingTools_DestroyHostWindow(void* windowPtr)
{
    if (windowPtr == NULL)
    {
        return;
    }

    NSWindow* window = (__bridge_transfer NSWindow*)windowPtr;
    PrintingToolsRunOnMainThread(^{
        if (window != nil)
        {
            [window orderOut:nil];
            [window close];
        }
    });
}

void* PrintingTools_CreateManagedPreviewHost(void)
{
    __block PrintingToolsManagedPreviewHost* host = nil;
    PrintingToolsRunOnMainThread(^{
        host = [[PrintingToolsManagedPreviewHost alloc] init];
    });

    return host != nil ? (__bridge_retained void*)host : NULL;
}

void PrintingTools_DestroyManagedPreviewHost(void* hostPtr)
{
    if (hostPtr == NULL)
    {
        return;
    }

    PrintingToolsRunOnMainThread(^{
        PrintingToolsManagedPreviewHost* host = (__bridge_transfer PrintingToolsManagedPreviewHost*)hostPtr;
        (void)host;
    });
}

void* PrintingTools_GetManagedPreviewView(void* hostPtr)
{
    if (hostPtr == NULL)
    {
        return NULL;
    }

    __block NSView* view = nil;
    PrintingToolsRunOnMainThread(^{
        PrintingToolsManagedPreviewHost* host = (__bridge PrintingToolsManagedPreviewHost*)hostPtr;
        view = host.containerView;
    });

    return (__bridge void*)view;
}

void PrintingTools_UpdateManagedPreviewWithPdf(void* hostPtr, const void* data, int length, double dpi)
{
    if (hostPtr == NULL || data == NULL || length <= 0)
    {
        return;
    }

    PrintingToolsRunOnMainThread(^{
        PrintingToolsManagedPreviewHost* host = (__bridge PrintingToolsManagedPreviewHost*)hostPtr;
        if (host == nil)
        {
            return;
        }

        NSData* pdfData = [NSData dataWithBytes:data length:(NSUInteger)length];
        [host updateWithPdfData:pdfData dpi:dpi];
        [host resetLayout];
    });
}

void PrintingTools_SetWindowContent(void* windowPtr, void* viewPtr)
{
    if (windowPtr == NULL)
    {
        return;
    }

    PrintingToolsRunOnMainThread(^{
        NSWindow* window = (__bridge NSWindow*)windowPtr;
        NSView* view = viewPtr != NULL ? (__bridge NSView*)viewPtr : nil;
        if (window != nil)
        {
            if (view != nil)
            {
                view.frame = window.contentView.bounds;
                view.autoresizingMask = NSViewWidthSizable | NSViewHeightSizable;
            }
            window.contentView = view;
        }
    });
}

void PrintingTools_DrawBitmap(
    void* cgContext,
    const void* pixels,
    int width,
    int height,
    int stride,
    double destX,
    double destY,
    double destWidth,
    double destHeight,
    int pixelFormat,
    int colorSpaceCode)
{
    if (cgContext == NULL || pixels == NULL || width <= 0 || height <= 0 || stride <= 0)
    {
        return;
    }

    CGContextRef context = (CGContextRef)cgContext;
    CGColorSpaceRef colorSpace = PrintingToolsCreateColorSpace(colorSpaceCode);
    if (colorSpace == NULL)
    {
        return;
    }

    CGBitmapInfo bitmapInfo = kCGBitmapByteOrder32Little | kCGImageAlphaPremultipliedFirst;
    if (pixelFormat == 1)
    {
        bitmapInfo = kCGBitmapByteOrder32Big | kCGImageAlphaPremultipliedLast;
    }

    size_t dataSize = (size_t)stride * (size_t)height;
    CGDataProviderRef provider = CGDataProviderCreateWithData(NULL, pixels, dataSize, NULL);
    if (provider == NULL)
    {
        CGColorSpaceRelease(colorSpace);
        return;
    }

    CGImageRef image = CGImageCreate(
        width,
        height,
        8,
        32,
        stride,
        colorSpace,
        bitmapInfo,
        provider,
        NULL,
        true,
        kCGRenderingIntentDefault);

    CGDataProviderRelease(provider);
    CGColorSpaceRelease(colorSpace);

    if (image == NULL)
    {
        return;
    }

    CGRect destination = CGRectMake(destX, destY, destWidth, destHeight);
    NSGraphicsContext* nsContext = [NSGraphicsContext currentContext];
    BOOL isContextFlipped = nsContext != nil ? nsContext.isFlipped : NO;
    CGContextSaveGState(context);
    CGContextSetInterpolationQuality(context, kCGInterpolationHigh);
    CGContextTranslateCTM(context, destination.origin.x, destination.origin.y);
    if (isContextFlipped)
    {
        CGContextScaleCTM(context,
            destination.size.width / (double)width,
            destination.size.height / (double)height);
    }
    else
    {
        CGContextTranslateCTM(context, 0, destination.size.height);
        CGContextScaleCTM(context,
            destination.size.width / (double)width,
            -destination.size.height / (double)height);
    }

    CGContextDrawImage(context, CGRectMake(0, 0, width, height), image);
    CGContextRestoreGState(context);

    CGImageRelease(image);
}

int PrintingTools_RunVectorPreview(const PrintingToolsVectorDocument* document)
{
    if (document == NULL || document->pdfBytes == NULL || document->length <= 0)
    {
        return 0;
    }

    return PrintingTools_RunPdfPrintOperation(document->pdfBytes, document->length, document->showPrintPanel);
}

PrintingToolsStringArray PrintingTools_GetPrinterNames(void)
{
    PrintingToolsStringArray result;
    result.names = NULL;
    result.lengths = NULL;
    result.count = 0;

    @autoreleasepool
    {
        NSArray<NSString*>* printers = [NSPrinter printerNames];
        NSInteger count = printers.count;
        if (count <= 0)
        {
            return result;
        }

        const unichar** names = (const unichar**)calloc((size_t)count, sizeof(unichar*));
        int* lengths = (int*)calloc((size_t)count, sizeof(int));
        if (names == NULL || lengths == NULL)
        {
            if (names != NULL)
            {
                free((void*)names);
            }
            if (lengths != NULL)
            {
                free((void*)lengths);
            }
            return result;
        }

        for (NSInteger i = 0; i < count; i++)
        {
            NSString* name = printers[i];
            if (name == nil)
            {
                names[i] = NULL;
                lengths[i] = 0;
                continue;
            }

            NSUInteger length = name.length;
            lengths[i] = (int)length;
            if (length == 0)
            {
                names[i] = NULL;
                continue;
            }

            unichar* buffer = (unichar*)malloc(sizeof(unichar) * length);
            if (buffer == NULL)
            {
                names[i] = NULL;
                lengths[i] = 0;
                continue;
            }

            [name getCharacters:buffer range:NSMakeRange(0, length)];
            names[i] = buffer;
        }

        result.names = names;
        result.lengths = lengths;
        result.count = (int)count;
    }

    return result;
}

void PrintingTools_FreePrinterNames(PrintingToolsStringArray array)
{
    if (array.names != NULL)
    {
        for (int i = 0; i < array.count; i++)
        {
            if (array.names[i] != NULL)
            {
                free((void*)array.names[i]);
            }
        }
        free((void*)array.names);
    }

    if (array.lengths != NULL)
    {
        free((void*)array.lengths);
    }
}
static void PrintingToolsRunOnMainThread(dispatch_block_t block)
{
    if (block == nil)
    {
        return;
    }

    if ([NSThread isMainThread])
    {
        block();
    }
    else
    {
        dispatch_sync(dispatch_get_main_queue(), block);
    }
}

static NSString* PrintingToolsColorModeString(int colorMode)
{
    switch (colorMode)
    {
        case 2:
            return NSPrintColorModeColor;
        case 1:
            return NSPrintColorModeGray;
        default:
            return nil;
    }
}

static int PrintingToolsColorModeFromString(NSString* colorMode)
{
    if (colorMode == nil)
    {
        return 0;
    }

    if ([colorMode caseInsensitiveCompare:NSPrintColorModeColor] == NSOrderedSame)
    {
        return 2;
    }

    if ([colorMode caseInsensitiveCompare:NSPrintColorModeGray] == NSOrderedSame ||
        [colorMode caseInsensitiveCompare:NSPrintColorModeBlackAndWhite] == NSOrderedSame)
    {
        return 1;
    }

    return 0;
}

static NSNumber* PrintingToolsDuplexNumber(int duplexMode)
{
    switch (duplexMode)
    {
        case 1:
            return @(NSPrintDuplexLongEdge);
        case 2:
            return @(NSPrintDuplexShortEdge);
        default:
            return @(NSPrintDuplexNone);
    }
}

static int PrintingToolsDuplexFromNumber(NSNumber* value)
{
    if (value == nil)
    {
        return 0;
    }

    switch (value.integerValue)
    {
        case NSPrintDuplexLongEdge:
            return 1;
        case NSPrintDuplexShortEdge:
            return 2;
        default:
            return 0;
    }
}

static void PrintingToolsApplyPaperPreset(NSPrintInfo* info, NSSize targetSize, PrintingToolsOperationHost* host)
{
    if (info == nil)
    {
        return;
    }

    typedef struct
    {
        NSString* paperName;
        double width;
        double height;
    } PrintingToolsPaperPreset;

    static const PrintingToolsPaperPreset Presets[] = {
        { NSPaperNameLetter, 612.0, 792.0 },
        { NSPaperNameLegal, 612.0, 1008.0 },
        { NSPaperNameA4, 595.0, 842.0 },
        { NSPaperNameTabloid, 792.0, 1224.0 }
    };

    const PrintingToolsPaperPreset* bestPreset = NULL;
    CGFloat bestDelta = CGFLOAT_MAX;

    for (size_t i = 0; i < sizeof(Presets) / sizeof(PrintingToolsPaperPreset); i++)
    {
        const PrintingToolsPaperPreset* preset = &Presets[i];
        CGFloat delta = fabs(preset->width - targetSize.width) + fabs(preset->height - targetSize.height);
        CGFloat swapped = fabs(preset->width - targetSize.height) + fabs(preset->height - targetSize.width);
        CGFloat minDelta = MIN(delta, swapped);
        if (minDelta < bestDelta)
        {
            bestDelta = minDelta;
            bestPreset = preset;
        }
    }

    if (bestPreset != NULL)
    {
        info.paperName = bestPreset->paperName;
        if (host != nil)
        {
            host.lastPaperName = bestPreset->paperName;
        }
    }

    info.paperSize = targetSize;
}

static CGColorSpaceRef PrintingToolsCreateColorSpace(int code)
{
    if (code == 2)
    {
        CGColorSpaceRef p3 = CGColorSpaceCreateWithName(kCGColorSpaceDisplayP3);
        if (p3 != NULL)
        {
            return p3;
        }
    }

    if (code == 1)
    {
        CGColorSpaceRef srgb = CGColorSpaceCreateWithName(kCGColorSpaceSRGB);
        if (srgb != NULL)
        {
            return srgb;
        }
    }

    return CGColorSpaceCreateDeviceRGB();
}
