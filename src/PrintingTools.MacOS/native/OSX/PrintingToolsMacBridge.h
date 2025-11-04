#pragma once

#ifdef __cplusplus
extern "C" {
#endif

void* PrintingTools_CreatePrintOperation(void* context);
void  PrintingTools_DisposePrintOperation(void* operation);
void  PrintingTools_BeginPreview(void* operation);
void  PrintingTools_CommitPrint(void* operation);
int   PrintingTools_ShowPrintPanel(void* operation, const void* options);
int   PrintingTools_ShowPrintPanelSheet(void* operation, void* window, const void* options);
int   PrintingTools_ShowPageLayout(void* operation, const void* options);
int   PrintingTools_GetPrintInfo(void* operation, void* info);
int   PrintingTools_RunModalPrintOperation(void* operation);
int   PrintingTools_RunPdfPrintOperation(const void* pdfData, int length, int showPrintPanel);
int   PrintingTools_RunVectorPreview(const void* document);
void* PrintingTools_GetPreviewView(void* operation);
void* PrintingTools_CreateHostWindow(double width, double height);
void  PrintingTools_ShowWindow(void* window);
void  PrintingTools_DestroyHostWindow(void* window);
void* PrintingTools_CreateManagedPreviewHost(void);
void  PrintingTools_DestroyManagedPreviewHost(void* host);
void* PrintingTools_GetManagedPreviewView(void* host);
void  PrintingTools_UpdateManagedPreviewWithPdf(void* host, const void* data, int length, double dpi);
void  PrintingTools_SetWindowContent(void* window, void* view);
void  PrintingTools_DrawBitmap(void* cgContext, const void* pixels, int width, int height, int stride, double destX, double destY, double destWidth, double destHeight, int pixelFormat, int colorSpace);

#ifdef __cplusplus
}
#endif
