import { Component, OnInit } from '@angular/core';
import { from, of } from 'rxjs';
import { catchError, concatMap, map, mergeMap } from 'rxjs/operators';
import { StorageService, UploadResponse, FileMetadata } from './storage.service';

interface UploadItem {
  file: File;
  status: 'pending' | 'uploading' | 'done' | 'error';
  errorMessage?: string;
}

type SortField = 'name' | 'date';

// 批次上傳/刪除的最大併發數，避免一次選取大量檔案時塞爆瀏覽器/伺服器
const BATCH_CONCURRENCY = 3;

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
  sortField: SortField = 'date'; // 目前排序欄位：name = 檔案名稱，date = 上傳日期
  sortDescending: boolean = true; // 排序方向：true = 降序，false = 升序
  selectedForDownload = new Set<string>(); // 勾選要批次下載/刪除的 uuidFileName
  isBatchDownloading: boolean = false;
  isBatchDeleting: boolean = false;

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
      this.sortFiles();
      this.isLoading = false;
    });
  }

  sortFiles() {
    this.filesWithMetadata.sort((a, b) => {
      const comparison = this.sortField === 'name'
        ? a.originalFileName.localeCompare(b.originalFileName, undefined, { sensitivity: 'base' })
        : new Date(a.uploadDate).getTime() - new Date(b.uploadDate).getTime();

      return this.sortDescending ? -comparison : comparison;
    });
  }

  setSortField(field: SortField) {
    if (this.sortField === field) {
      this.sortDescending = !this.sortDescending;
    } else {
      this.sortField = field;
      // 名稱預設 A→Z，日期預設最新在前
      this.sortDescending = field === 'date';
    }
    this.sortFiles();
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
      }, BATCH_CONCURRENCY)
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

  toggleFileSelection(uuidFileName: string) {
    if (this.selectedForDownload.has(uuidFileName)) {
      this.selectedForDownload.delete(uuidFileName);
    } else {
      this.selectedForDownload.add(uuidFileName);
    }
  }

  isFileSelected(uuidFileName: string): boolean {
    return this.selectedForDownload.has(uuidFileName);
  }

  get allFilesSelected(): boolean {
    return this.filesWithMetadata.length > 0 &&
      this.filesWithMetadata.every(f => this.selectedForDownload.has(f.uuidFileName));
  }

  toggleSelectAll() {
    if (this.allFilesSelected) {
      this.selectedForDownload.clear();
    } else {
      this.filesWithMetadata.forEach(f => this.selectedForDownload.add(f.uuidFileName));
    }
  }

  downloadSelectedFiles() {
    const filesToDownload = this.filesWithMetadata.filter(f => this.selectedForDownload.has(f.uuidFileName));
    if (filesToDownload.length === 0) {
      return;
    }

    this.isBatchDownloading = true;

    // 依序下載（concatMap），避免瀏覽器把短時間內大量觸發的下載視為彈出視窗而擋下
    from(filesToDownload).pipe(
      concatMap(file => this.storageService.downloadFileAsBlob(file.uuidFileName).pipe(
        map(blob => ({ file, blob, error: null as any })),
        catchError(error => of({ file, blob: null as Blob | null, error }))
      ))
    ).subscribe({
      next: ({ file, blob, error }) => {
        if (error || !blob) {
          console.error('Download failed:', file.originalFileName, error);
          return;
        }
        const a = document.createElement('a');
        const objectUrl = URL.createObjectURL(blob);
        a.href = objectUrl;
        a.download = file.originalFileName;
        a.click();
        URL.revokeObjectURL(objectUrl);
      },
      complete: () => {
        this.isBatchDownloading = false;
      }
    });
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
      this.selectedForDownload.delete(uuidFileName);
      this.loadFiles();
    });
  }

  deleteSelectedFiles() {
    const filesToDelete = this.filesWithMetadata.filter(f => this.selectedForDownload.has(f.uuidFileName));
    if (filesToDelete.length === 0) {
      return;
    }

    const confirmed = window.confirm(`確定要刪除選取的 ${filesToDelete.length} 個檔案嗎？此操作無法復原。`);
    if (!confirmed) {
      return;
    }

    this.isBatchDeleting = true;

    from(filesToDelete).pipe(
      mergeMap(file => this.storageService.deleteFile(file.uuidFileName).pipe(
        map(() => ({ file, error: null as any })),
        catchError(error => of({ file, error }))
      ), BATCH_CONCURRENCY)
    ).subscribe({
      next: ({ file, error }) => {
        if (error) {
          console.error('Delete failed:', file.originalFileName, error);
          return;
        }
        this.uploadedFiles = this.uploadedFiles.filter(f => f.uuidFileName !== file.uuidFileName);
        this.selectedForDownload.delete(file.uuidFileName);
      },
      complete: () => {
        this.isBatchDeleting = false;
        this.loadFiles();
      }
    });
  }
}
