export interface OlapRequest {
  Cube?: string
  Measure?: string
  ActiveDimension?: 'ThoiGian' | 'KhachHang' | 'DiaDiem'
  ActiveDimensions?: Array<'ThoiGian' | 'KhachHang' | 'DiaDiem' | 'MatHang'>
  DimensionLevels?: Partial<Record<'ThoiGian' | 'KhachHang' | 'DiaDiem' | 'MatHang', string>>
  ProductKey?: string
  CustomerKey?: string
  CustomerType?: string
  CustomerName?: string
  StoreKey?: string
  State?: string
  City?: string
  Year?: number
  Quarter?: number
  Month?: number
  RowLevel?: string
  ColLevel?: string
  IncludeSoLuong?: boolean
}

export interface SliceDiceRequest extends OlapRequest {
  IsDice?: boolean
}

export interface OlapResult {
  Success: boolean
  Error?: string
  Columns: string[]
  Rows: Record<string, unknown>[]
  Mdx?: string
  CurrentLevel: string
  OperationType: string
}

export interface MemberInfo {
  Key: string
  Name: string
}

export interface MetadataResult {
  Years: MemberInfo[]
  Products: MemberInfo[]
  Customers: MemberInfo[]
  Stores: MemberInfo[]
  Measures: string[]
  Cubes: string[]
  CubeInfos: CubeInfo[]
}

export interface CubeCapabilities {
  HasTime: boolean
  HasProduct: boolean
  HasCustomer: boolean
  HasStore: boolean
  AllowDrillDown: boolean
  AllowRollUp: boolean
  AllowSlice: boolean
  AllowDice: boolean
  AllowPivot: boolean
}

export interface CubeInfo {
  Name: string
  Fact: string
  DimensionCount: number
  Dimensions: string[]
  Measures: string[]
  Description: string
  Capabilities: CubeCapabilities
}

export interface CubeMappingItem {
  cube: string
  fact: 'BanHang' | 'TonKho'
  dimensions: string[]
}
