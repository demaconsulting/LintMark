// Views rendered into docs/design/generated/ for the Design document.
// Declares the single repository-level views package, which imports the LintMark system facade (it re-exports every
// subsystem and unit package) so each 'expose' statement can reference its target part def directly.
// Per SysML2Tools syntax, 'expose' is only valid inside a named 'view' usage (not a 'view def' definition), and it -
// not 'render' - is what scopes the rendered diagram content.
package LintMarkDesignViews {
    private import StandardViewDefinitions::*;
    private import LintMark::*;

    view SoftwareStructureView : InterconnectionView {
        expose LintMark::LintMarkSystem;
        filter not (@SysML::Documentation);
    }

    view LintMarkView {
        expose LintMarkSystem;
    }

    view DocumentView {
        expose DocumentSubsystem;
    }

    view EngineView {
        expose EngineSubsystem;
    }

    view ConfigurationView {
        expose ConfigurationSubsystem;
    }

    view LintingView {
        expose LintingSubsystem;
    }

    view FormattingView {
        expose FormattingSubsystem;
    }

    view BlocksView {
        expose BlocksSubsystem;
    }

    view InlineView {
        expose InlineSubsystem;
    }
}
