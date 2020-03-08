#ifndef MATH_CONVERT_INCLUDED
#define MATH_CONVERT_INCLUDED

float f16tof32(uint x)
{
    const uint shifted_exp = (0x7c00 << 13);
    uint uf = (x & 0x7fff) << 13;
    uint e = uf & shifted_exp;
    uf += (127 - 15) << 23;
    uf += lerp(0, (128u - 16u) << 23, e == shifted_exp);
    uf = lerp(uf, asuint(asfloat(uf + (1 << 23)) - 6.10351563e-05f), e == 0);
    uf |= (x & 0x8000) << 16;
    return asfloat(uf);
}

float2 uintToFloat2(uint input) {
    return float2(f16tof32(input & 0x0000FFFF), f16tof32((input & 0xFFFF0000) >> 16));
}

float4 uint2ToFloat4(uint2 input) {
    float2 xy = uintToFloat2(input.x);
    float2 zw = uintToFloat2(input.y);
    return float4(xy.x, xy.y, zw.x, zw.y);
}

float4x4 uint4x2ToFloat4x4(uint4x2 input) {
    float4 c0 = uint2ToFloat4(float2(input[0].x, input[1].x));
    float4 c2 = uint2ToFloat4(float2(input[0].y, input[1].y));
    float4 c1 = uint2ToFloat4(float2(input[2].x, input[3].x));
    float4 c3 = uint2ToFloat4(float2(input[2].y, input[3].y));
    return float4x4(
        c0.x, c1.x, c2.x, c3.x,
        c0.y, c1.y, c2.y, c3.y,
        c0.z, c1.z, c2.z, c3.z,
        c0.w, c1.w, c2.w, c3.w);
}
#endif