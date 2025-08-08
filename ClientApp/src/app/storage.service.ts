import { Injectable } from '@angular/core';
import { HttpClient, HttpHeaders, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { environment } from '../environments/environment';

// 定義上傳回應的介面
export interface UploadResponse {
  uuidFileName: string;
  originalFileName: string;
}

// 定義檔案資訊介面
export interface FileMetadata {
  uuidFileName: string;
  originalFileName: string;
  size: number;
  uploadDate: string;
}

@Injectable({
  providedIn: 'root'
})
export class StorageService {

  private apiUrl = `${environment.apiUrl}/storage`;

  constructor(private http: HttpClient) { }

  getFileList(): Observable<any> {
    return this.http.get<any>(this.apiUrl);
  }

  getFileListWithMetadata(): Observable<FileMetadata[]> {
    return this.http.get<FileMetadata[]>(`${this.apiUrl}/metadata`);
  }

  uploadFile(file: File): Observable<UploadResponse> {
    const formData = new FormData();
    formData.append('file', file, file.name);
    return this.http.post<UploadResponse>(this.apiUrl, formData);
  }

  downloadFile(uuidFileName: string): void {
    // 直接開啟下載連結，讓瀏覽器處理檔案下載
    const downloadUrl = `${this.apiUrl}/${uuidFileName}`;
    window.open(downloadUrl, '_blank');
  }

  // 如果需要通過 Observable 方式處理下載
  downloadFileAsBlob(uuidFileName: string): Observable<Blob> {
    return this.http.get(`${this.apiUrl}/${uuidFileName}`, {
      responseType: 'blob'
    });
  }

  deleteFile(uuidFileName: string): Observable<any> {
    return this.http.delete(`${this.apiUrl}/${uuidFileName}`);
  }
}
