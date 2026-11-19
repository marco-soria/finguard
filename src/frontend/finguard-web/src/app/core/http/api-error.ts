/** An HTTP failure in a shape the UI can show. Only `title` reaches the user (D-80). */
export class ApiError {
  constructor(
    readonly status: number,
    readonly title: string,
    readonly code?: string,
    readonly traceId?: string,
  ) {}
}
