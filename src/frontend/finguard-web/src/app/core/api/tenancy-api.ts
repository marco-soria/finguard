import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE } from './api-base';

export interface CurrentTenant {
  tenantId: string;
  tenantName: string;
}

@Injectable({ providedIn: 'root' })
export class TenancyApi {
  private readonly http = inject(HttpClient);

  getCurrentTenant(): Observable<CurrentTenant> {
    return this.http.get<CurrentTenant>(`${API_BASE}/tenancy/me`);
  }
}
