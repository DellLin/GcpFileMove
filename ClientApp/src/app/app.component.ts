import { Component, OnInit } from '@angular/core';
import { from, of } from 'rxjs';
import { catchError, map, mergeMap } from 'rxjs/operators';
import { StorageService, UploadResponse, FileMetadata } from './storage.service';

interface UploadItem {
  file: File;
  status: 'pending' | 'uploading' | 'done' | 'error';
  errorMessage?: string;
}

// 同時上傳的最大併發數，避免一次選取大量檔案時塞爆瀏覽器/伺服器
const UPLOAD_CONCURRENCY = 3;

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {
  title = 'app';
  files: any[] = [];
  filesWithMetadata: FileMetadata[] = []; // 帶有原始檔案名稱的檔案列表
  selectedFiles: UploadItem[] = [];
  uploadedFiles: { uuidFileName: string, originalFileName: string }[] = []; // 儲存上傳的檔案資訊
  sortDescending: boolean = true; // 排序方向：true = 降序（最新在前），false = 升序（最舊在前）

  // UI state management
  isUploading: boolean = false;
  uploadProgress: number = 0;
  isLoading: boolean = false;

  constructor(private storageService: StorageService) { }

  ngOnInit() {
    this.loadFiles();
  }

  loadFiles() {
    this.isLoading = true;

    // 載入原始的檔案列表（舊版 API）
    this.storageService.getFileList().subscribe(data => {
      this.files = data.items || [];
    });

    // 載入帶有元數據的檔案列表（新版 API）
    this.storageService.getFileListWithMetadata().subscribe(data => {
      this.filesWithMetadata = data;
      this.sortFilesByDate();
      this.isLoading = false;
    });
  }

  sortFilesByDate() {
    this.filesWithMetadata.sort((a, b) => {
      const dateA = new Date(a.uploadDate).getTime();
      const dateB = new Date(b.uploadDate).getTime();
      return this.sortDescending ? (dateB - dateA) : (dateA - dateB);
    });
  }

  toggleSortOrder() {
    this.sortDescending = !this.sortDescending;
    this.sortFilesByDate();
  } onFileSelected(event: any) {
    const files: FileList = event.target.files;
    this.selectedFiles = Array.from(files || []).map(file => ({ file, status: 'pending' as const }));
  }

  removeSelectedFile(index: number) {
    this.selectedFiles.splice(index, 1);
  }

  onUpload() {
    // 只上傳尚未成功的檔案，讓失敗的項目可以重試而不必重新選擇全部檔案
    const pendingItems = this.selectedFiles.filter(item => item.status === 'pending' || item.status === 'error');
    if (pendingItems.length === 0) {
      return;
    }

    this.isUploading = true;

    from(pendingItems).pipe(
      mergeMap(item => {
        item.status = 'uploading';
        item.errorMessage = undefined;
        return this.storageService.uploadFile(item.file).pipe(
          map((response: UploadResponse) => ({ item, response, error: null as any })),
          catchError(error => of({ item, response: null as UploadResponse | null, error }))
        );
      }, UPLOAD_CONCURRENCY)
    ).subscribe({
      next: ({ item, response, error }) => {
        if (error || !response) {
          item.status = 'error';
          item.errorMessage = error?.message || 'Upload failed';
          console.error('Upload failed:', item.file.name, error);
        } else {
          item.status = 'done';
          this.uploadedFiles.push({
            uuidFileName: response.uuidFileName,
            originalFileName: response.originalFileName
          });
        }
      },
      complete: () => {
        this.isUploading = false;
        this.loadFiles();
        // 清除已成功上傳的項目，保留失敗的項目供使用者重試
        this.selectedFiles = this.selectedFiles.filter(item => item.status !== 'done');

        // Reset file input
        const fileInput = document.getElementById('fileInput') as HTMLInputElement;
        if (fileInput) {
          fileInput.value = '';
        }
      }
    });
  }

  downloadFile(uuidFileName: string) {
    // 使用簡化的下載方法，直接開啟下載連結
    this.storageService.downloadFile(uuidFileName);
  }

  // 如果需要更複雜的下載處理，可以使用這個方法
  downloadFileAsBlob(uuidFileName: string) {
    this.storageService.downloadFileAsBlob(uuidFileName).subscribe((blob: Blob) => {
      const a = document.createElement('a');
      const objectUrl = URL.createObjectURL(blob);
      a.href = objectUrl;
      a.download = `downloaded_${uuidFileName}`; // 使用 UUID 作為預設檔案名稱
      a.click();
      URL.revokeObjectURL(objectUrl);
    });
  }

  deleteFile(uuidFileName: string) {
    this.storageService.deleteFile(uuidFileName).subscribe(() => {
      // 從本地列表中移除已刪除的檔案
      this.uploadedFiles = this.uploadedFiles.filter(f => f.uuidFileName !== uuidFileName);
      this.loadFiles();
    });
  }
}
