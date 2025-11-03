#import <AppKit/AppKit.h>
#include <stdlib.h>

@class PrintingToolsOperationHost;

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
} PrintingToolsManagedPrintSettings;

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
- (instancetype)initWithContext:(void*)context callbacks:(PrintingToolsManagedCallbacks)callbacks;
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
    }
    return self;
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
    host.operation = nil;
    host.previewView = nil;
}

void PrintingTools_ConfigurePrintOperation(void* operation, const PrintingToolsManagedPrintSettings* settings)
{
    if (operation == NULL || settings == NULL)
    {
        return;
    }

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

    info.paperSize = NSMakeSize(settings->paperWidth, settings->paperHeight);
    info.leftMargin = settings->marginLeft;
    info.topMargin = settings->marginTop;
    info.rightMargin = settings->marginRight;
    info.bottomMargin = settings->marginBottom;

    info.orientation = settings->orientation == 1 ? NSPaperOrientationLandscape : NSPaperOrientationPortrait;

    if (attributes != nil)
    {
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
    int pixelFormat)
{
    if (cgContext == NULL || pixels == NULL || width <= 0 || height <= 0 || stride <= 0)
    {
        return;
    }

    CGContextRef context = (CGContextRef)cgContext;
    CGColorSpaceRef colorSpace = CGColorSpaceCreateDeviceRGB();
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
