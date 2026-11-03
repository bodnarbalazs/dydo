/* Copyright (c) 2026 Balazs Bodnar. SPDX-License-Identifier: MIT */
package tlc2;

import java.nio.charset.StandardCharsets;
import java.util.Base64;

/** Forwarding/error control only; never TLC semantic or product-path evidence. */
public final class TLC {
    private TLC() { }

    public static void main(String[] arguments) {
        System.out.println("CONTROL_TLC_ENTERED " + arguments.length);
        for (int index = 0; index < arguments.length; index++) {
            System.out.println(index + ":" + Base64.getEncoder().encodeToString(arguments[index].getBytes(StandardCharsets.UTF_8)));
        }
        System.err.println("CONTROL_STDERR");
        if ("throw".equals(System.getenv("STATECHART_CONTROL_EXIT"))) {
            throw new IllegalStateException("CONTROL_THROW");
        }
        System.exit(Integer.parseInt(System.getenv("STATECHART_CONTROL_EXIT")));
    }
}
