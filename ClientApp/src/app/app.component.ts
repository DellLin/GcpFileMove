import { Component, OnInit } from '@angular/core';
import { StorageService, UploadResponse, FileMetadata } from './storage.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html'
})
export class AppComponent implements OnInit {
  title = 'app';
  files: any[] = [];
  filesWithMetadata: FileMetadata[] = []; // 帶有原始檔案名稱的檔案列表
  selectedFile: File | null = null;
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
    this.selectedFile = event.target.files[0];
  }

  onUpload() {
    if (this.selectedFile) {
      this.isUploading = true;
      this.storageService.uploadFile(this.selectedFile).subscribe((response: UploadResponse) => {
        // 將上傳的檔案資訊加入到列表中
        this.uploadedFiles.push({
          uuidFileName: response.uuidFileName,
          originalFileName: response.originalFileName
        });
        this.loadFiles();
        this.selectedFile = null;
        this.isUploading = false;

        // Reset file input
        const fileInput = document.getElementById('fileInput') as HTMLInputElement;
        if (fileInput) {
          fileInput.value = '';
        }
      }, error => {
        this.isUploading = false;
        console.error('Upload failed:', error);
      });
    }
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
