// Views rendered into docs/design/generated/ for the Design document.
// Reopens the LintMark package so each 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
package LintMark {
    view SoftwareStructureView {
        expose LintMark;
    }

    view LintMarkView {
        expose LintMarkSystem;
    }

    view CliView {
        expose CliSubsystem;
    }

    view SelfTestView {
        expose SelfTestSubsystem;
    }

    view UtilitiesView {
        expose UtilitiesSubsystem;
    }

    view DocumentView {
        expose DocumentSubsystem;
    }

    view EngineView {
        expose EngineSubsystem;
    }
}
