import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

export interface CurrentTenant {
  tenantId: string;
  tenantName: string;
}

@Injectable({ providedIn: 'root' })
export class TenancyApi {
  private readonly http = inject(HttpClient);

  getCurrentTenant(): Observable<CurrentTenant> {
    return this.http.get<CurrentTenant>('/api/tenancy/me');
  }
}
