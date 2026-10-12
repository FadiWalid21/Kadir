import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface PayrollRuleListItem {
  ruleKey: string;
  valueType: string;
  valueJson: string;
  effectiveFrom: string;
  isCustomized: boolean;
  isVerified: boolean;
  differsFromOfficialDefault: boolean;
  updateAvailable: boolean;
  officialDefaultJson: string | null;
  reason: string;
  id: string;
}

export interface PayrollRulesList {
  items: PayrollRuleListItem[];
  rulesConfirmedForLock: boolean;
  rulesConfirmedAt: string | null;
  rulesConfirmedReason: string | null;
}

export interface PayrollRuleHistoryItem {
  id: string;
  valueJson: string;
  effectiveFrom: string;
  isCustomized: boolean;
  isVerified: boolean;
  reason: string;
  createdAt: string;
  createdBy: string | null;
}

export interface UpsertPayrollRuleBody {
  valueJson: string;
  effectiveFrom: string;
  reason: string;
}

export interface ConfirmPayrollRulesBody {
  reason: string;
}

export interface ConfirmPayrollRulesResult {
  rulesConfirmedForLock: boolean;
  rulesConfirmedAt: string;
  reason: string;
}

@Injectable({ providedIn: 'root' })
export class PayrollRulesApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${environment.apiBaseUrl}/payroll/rules`;

  list(): Promise<PayrollRulesList> {
    return firstValueFrom(this.http.get<PayrollRulesList>(this.base));
  }

  history(ruleKey: string): Promise<PayrollRuleHistoryItem[]> {
    return firstValueFrom(
      this.http.get<PayrollRuleHistoryItem[]>(`${this.base}/${encodeURIComponent(ruleKey)}/history`),
    );
  }

  upsert(ruleKey: string, body: UpsertPayrollRuleBody): Promise<PayrollRuleListItem> {
    return firstValueFrom(
      this.http.put<PayrollRuleListItem>(`${this.base}/${encodeURIComponent(ruleKey)}`, body),
    );
  }

  reset(ruleKey: string, body: UpsertPayrollRuleBody): Promise<PayrollRuleListItem> {
    return firstValueFrom(
      this.http.post<PayrollRuleListItem>(
        `${this.base}/${encodeURIComponent(ruleKey)}/reset`,
        body,
      ),
    );
  }

  confirm(body: ConfirmPayrollRulesBody): Promise<ConfirmPayrollRulesResult> {
    return firstValueFrom(
      this.http.post<ConfirmPayrollRulesResult>(`${this.base}/confirm`, body),
    );
  }
}
